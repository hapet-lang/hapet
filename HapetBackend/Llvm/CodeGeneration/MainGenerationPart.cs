using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using LLVMSharp;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;
using System.Xml.Linq;

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
			// entryTypes.Add(rttiTypeInfoPtr);

			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					// declaring global func
					var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);
					LLVMValueRef lfunc = _module.AddFunction($"{classDecl.Name.Name}::{funcDecl.Name.Name}", funcType);
					lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage; // TODO: external shite controlled here
					// caching the function
					_functionMap[funcDecl.Type.OutType as HapetFrontend.Types.FunctionType] = lfunc;

					// setting parameter names
					for (int i = 0; i < funcDecl.Parameters.Count; ++i)
					{
						var p = funcDecl.Parameters[i];
						lfunc.Params[i].Name = p.Name.Name;
					}

					// function body
					var bbBody = lfunc.AppendBasicBlock("entry");
					_builder.PositionAtEnd(bbBody);

					// ret if void
					if (funcDecl.Returns.OutType is VoidType)
					{
						// PopStackTrace(); // TODO: stack trace
						_builder.BuildRetVoid();
					}
					else
					{
						// TODO: return value
					}
				}
			}

			if (entryTypes.Count == 0)
			{
				// no need for class to be zero sized so add i8
				entryTypes.Add(_context.Int8Type.GetPointerTo());
			}

			var classStruct = _context.CreateNamedStruct($"class.{classDecl.Name.Name}");
			classStruct.StructSetBody(entryTypes.ToArray(), false);
		}

		private void GenerateBlockCode(AstBlockExpr block)
		{

		}
	}
}
