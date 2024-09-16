using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		// TODO: rewrite this shite

		private void GenerateCode()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						GenerateClassCode(classDecl);
					}
				}
			}
		}

		private unsafe void GenerateClassCode(AstClassDecl classDecl)
		{
			var entryTypes = new List<LLVMTypeRef>();

			// TODO: entry for type info
			entryTypes.Add(rttiTypeInfoPtr);

			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					_vtableIndices[funcDecl] = entryTypes.Count;

					var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);
					entryTypes.Add(funcType);
				}
			}

			var vtableType = _context.CreateNamedStruct($"__vtable_type_{classDecl.Type.OutType}");
			vtableType.StructSetBody(entryTypes.ToArray(), false);
			_vtableTypes[classDecl.Type.OutType] = vtableType;

			//// SetVTables
			//var entries = new LLVMValueRef[entryTypes.Count];
			//for (int i = 0; i < entries.Length; i++)
			//{
			//	entries[i] = (LLVMValueRef)LLVM.ConstNull(entryTypes[i]);
			//}

			// entries for functions
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					int index = _vtableIndices[funcDecl];
					ulong offset = _targetData.OffsetOfElement(vtableType, (uint)index);
					_vtableOffsets[funcDecl] = offset;

					// GenerateFunctionHeader
					var name = funcDecl.Name.Name;
					if (funcDecl.Body != null)
						name += ".hpt";

					LLVMTypeRef ltype = entryTypes[index];
					LLVMValueRef lfunc = _module.AddFunction(name, ltype);
					lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage; // TODO: external shite controlled here

					_valueMap[funcDecl] = lfunc;

					//// SetVTables
					//entries[index] = LLVM.ConstPointerCast(_valueMap[funcDecl], entryTypes[index]);

					// generate body
					{
						var builder = _context.CreateBuilder();
						this._builder = builder;

						var bbParams = lfunc.AppendBasicBlock("locals");
						var bbBody = lfunc.AppendBasicBlock("body");

						// allocate space for parameters and return values on stack
						builder.PositionAtEnd(bbParams);

						// PushStackTrace(function); // TODO: stack trace

						for (int i = 0; i < funcDecl.Parameters.Count; i++)
						{
							var param = funcDecl.Parameters[i];
							var p = lfunc.GetParam((uint)i);
							var ptype = LLVM.TypeOf(p);
							p = builder.BuildAlloca(ptype, $"p_{param.Name?.Name}");
							_valueMap[param] = p;
						}

						if (funcDecl.Returns != null)
						{
							var ptype = HapetTypeToLLVMType(funcDecl.Returns.OutType);
							string nameOfRet = (funcDecl.Returns is AstIdExpr) ? (funcDecl.Returns as AstIdExpr).Name : "anime"; // TODO: this shite is because of tuples
							var p = builder.BuildAlloca(ptype, $"ret_{nameOfRet}");
							_valueMap[funcDecl.Returns] = p;
						}

						// store params and rets in local variables
						for (int i = 0; i < funcDecl.Parameters.Count; i++)
						{
							var param = funcDecl.Parameters[i];
							var p = lfunc.GetParam((uint)i);
							builder.BuildStore(p, _valueMap[param]);
						}

						// temp values
						builder.BuildBr(bbBody);

						// body
						builder.PositionAtEnd(bbBody);
						GenerateExpression(funcDecl.Body, false);

						// ret if void
						if (funcDecl.Returns == null)
						{
							// PopStackTrace(); // TODO: stack trace
							builder.BuildRetVoid();
						}
						builder.Dispose();
					}

					// TODO: removing empty blocks


				}
			}

			//var defValue = LLVMValueRef.CreateConstNamedStruct(vtableType, entries);
			//var vtable = _vtableMap[impl];
			//LLVM.SetInitializer(vtable, defValue);
		}
	}
}
