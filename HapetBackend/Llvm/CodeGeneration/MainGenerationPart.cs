using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Parsing;
using HapetFrontend.Scoping;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Diagnostics;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private ProgramFile _currentSourceFile;

		private void GenerateCode()
		{
			foreach (var (path, file) in _compiler.GetFiles())
			{
				_currentSourceFile = file;

				foreach (var stmt in file.Statements)
				{
					if (stmt is AstClassDecl classDecl)
					{
						GenerateClassCode(classDecl);
					}
					else if (stmt is AstFuncDecl funcDecl)
					{
						// usually extern funcs
						GenerateFuncCode(funcDecl, null, null);
					}
				}
			}
		}

		private unsafe void GenerateClassCode(AstClassDecl classDecl)
		{
			// TODO: create using HapetTypeToLLVMType
			var classStruct = _context.CreateNamedStruct($"class.{classDecl.Name.Name}");
			_typeMap[classDecl.Type.OutType] = classStruct;

			var entryTypes = new List<LLVMTypeRef>();
			var entryHapetTypes = new List<HapetType>();
			var funcs = new Dictionary<AstFuncDecl, LLVMTypeRef>();

			// TODO: entry for type info
			entryTypes.Add(_context.Int8Type.GetPointerTo());
			entryHapetTypes.Add(PointerType.GetPointerType(IntType.GetIntType(1, false)));

            foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					// defining global func
					var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);
					funcs.Add(funcDecl, funcType);
				}
				else if (decl is AstVarDecl fieldDecl)
				{
					// TODO: save the types
                    entryTypes.Add(HapetTypeToLLVMType(fieldDecl.Type.OutType));
					entryHapetTypes.Add(fieldDecl.Type.OutType);
                }
			}

			// TODO: create using HapetTypeToLLVMType
			_structTypeElementsMap.Add(classDecl.Type.OutType, entryHapetTypes);
            classStruct.StructSetBody(entryTypes.ToArray(), false);
			// classDecl.Type.OutType.SetSizeAndAlignment(1, 4); // TODO: idk

			foreach (var (funcDecl, funcType) in funcs)
			{
				GenerateFuncCode(funcDecl, funcType, classDecl);
			}
		}

		private unsafe void GenerateFuncCode(AstFuncDecl funcDecl, LLVMTypeRef? funcType = null, AstClassDecl classDecl = null)
		{
			funcType ??= HapetTypeToLLVMType(funcDecl.Type.OutType);

			string funcName = classDecl != null ? $"{classDecl.Name.Name}::{funcDecl.Name.Name}" : funcDecl.Name.Name;

			// declaring global func
			LLVMValueRef lfunc = _module.AddFunction(funcName, funcType.Value);
			if (funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
				lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage; 
			else
				lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage;

			// caching the function											 
			_valueMap[funcDecl.GetSymbol] = lfunc;

			// setting parameter names
			for (int i = 0; i < funcDecl.Parameters.Count; ++i)
			{
				var p = funcDecl.Parameters[i];
				if (p.Name != null)
					lfunc.Params[i].Name = p.Name.Name;
			}

			// check if there is no implementation
			if (funcDecl.Body == null)
				return;

			// params body
			var paramsBody = lfunc.AppendBasicBlock("params");
			_builder.PositionAtEnd(paramsBody);
			// generating params allocs
			for (int i = 0; i < funcDecl.Parameters.Count; ++i)
			{
				var p = funcDecl.Parameters[i];
				var addrAlloca = _builder.BuildAlloca(HapetTypeToLLVMType(p.Type.OutType), $"{p.Name.Name}.addr");
				_builder.BuildStore(lfunc.GetParam((uint)i), addrAlloca);
				// var parArg = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), addrAlloca, $"{p.Name.Name}1");
				// _refMap[p.GetSymbol] = addrAlloca;
				// _valueMap[p.GetSymbol] = parArg;
				_valueMap[p.GetSymbol] = addrAlloca;
			}

			// function body
			var bbBody = lfunc.AppendBasicBlock("entry");
			_builder.BuildBr(bbBody);
			_builder.PositionAtEnd(bbBody);

			// genereting inside stuff of the function
			var retOfBlock = GenerateBlockCode(funcDecl.Body);

			// return logics
			if (retOfBlock != null)
			{
				// TODO: return value
				_builder.BuildRet(retOfBlock.Value);
			}
			else if (funcDecl.Returns.OutType is VoidType)
			{
				// ret if void
				// PopStackTrace(); // TODO: stack trace
				_builder.BuildRetVoid();
			}
			else
			{
				// error because the func is not void but with a type return
				// but the 'return' statement was not found
				_errorHandler.ReportError(_currentSourceFile.Text, funcDecl, "Return statement of the function could not be found");
				_builder.BuildRetVoid();
			}
			lfunc.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
		}

		private LLVMValueRef? GenerateBlockCode(AstBlockExpr blockExpr)
		{
			// TODO: put here return of the block if it exists
			LLVMValueRef? result = null;
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt is AstVarDecl varDecl)
				{
					GenerateVarDeclCode(varDecl);
				}
				else if (stmt is AstAssignStmt || stmt is AstCallExpr)
				{
					GenerateExpressionCode(stmt);
				}
				else if (stmt is AstReturnStmt returnStmt)
				{
					// TODO: also check if return expr is empty and method has to return smth - error it
					result = GenerateExpressionCode(returnStmt.ReturnExpression);
					break; // there is nothing to do in the block after return
				}
			}
			return result;
		}

		private void GenerateVarDeclCode(AstVarDecl varDecl)
		{
			// alloca new var in basicBlock
			var varPtr = CreateLocalVariable(varDecl.Type.OutType, varDecl.Name.Name);

			// check for initializer and try to evaluate expr
			if (varDecl.Initializer != null)
			{
				var x = GenerateExpressionCode(varDecl.Initializer);
				_builder.BuildStore(x, varPtr);
			}

			// _refMap[varDecl.GetSymbol] = varPtr;
			// _valueMap[varDecl.GetSymbol] = _builder.BuildLoad2(HapetTypeToLLVMType(varDecl.Type.OutType), varPtr, varDecl.Name.Name);
			_valueMap[varDecl.GetSymbol] = varPtr;
		}
	}
}
