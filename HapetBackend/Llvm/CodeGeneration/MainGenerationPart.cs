using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
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
				}
			}
		}

		private unsafe void GenerateClassCode(AstClassDecl classDecl)
		{
			// TODO: create using HapetTypeToLLVMType
			var classStruct = _context.CreateNamedStruct($"class.{classDecl.Name.Name}");
			_typeMap[classDecl.Type.OutType] = classStruct;

			var entryTypes = new List<LLVMTypeRef>();
			var funcs = new Dictionary<AstFuncDecl, LLVMTypeRef>();

			// TODO: entry for type info
			// entryTypes.Add(rttiTypeInfoPtr);

			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					// defining global func
					var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);
					funcs.Add(funcDecl, funcType);
				}
			}

			if (entryTypes.Count == 0)
			{
				// no need for class to be zero sized so add i8
				entryTypes.Add(_context.Int8Type.GetPointerTo());
			}

			// TODO: create using HapetTypeToLLVMType
			classStruct.StructSetBody(entryTypes.ToArray(), false);
			
			foreach (var (funcDecl, funcType) in funcs)
			{
				// declaring global func
				LLVMValueRef lfunc = _module.AddFunction($"{classDecl.Name.Name}::{funcDecl.Name.Name}", funcType);
				lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage; // TODO: external shite controlled here
																 // caching the function
				_valueMap[funcDecl.Type.OutType as HapetFrontend.Types.FunctionType] = lfunc;

				// setting parameter names
				for (int i = 0; i < funcDecl.Parameters.Count; ++i)
				{
					var p = funcDecl.Parameters[i];
					lfunc.Params[i].Name = p.Name.Name;
				}

				// check if there is no implementation
				if (funcDecl.Body == null)
					continue;

				// function body
				var bbBody = lfunc.AppendBasicBlock("entry");
				_builder.PositionAtEnd(bbBody);

				// genereting inside stuff of the function
				var retOfBlock = GenerateBlockCode(funcDecl.Body, bbBody);

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
		}

		private LLVMValueRef? GenerateBlockCode(AstBlockExpr blockExpr, LLVMBasicBlockRef basicBlock)
		{
			// TODO: put here return of the block if it exists
			LLVMValueRef? result = null;
			foreach (var stmt in blockExpr.Statements)
			{
				if (stmt is AstVarDecl varDecl)
				{
					GenerateVarDeclCode(varDecl, basicBlock);
				}
				else if (stmt is AstReturnStmt returnStmt)
				{
					// TODO: also check if return expr is empty
					result = GenerateExpressionCode(returnStmt.ReturnExpression, basicBlock, true);
					break; // there is nothing to do in the block after return
				}
			}
			return result;
		}

		private void GenerateVarDeclCode(AstVarDecl varDecl, LLVMBasicBlockRef basicBlock)
		{
			// alloca new var in basicBlock
			var varPtr = CreateLocalVariable(varDecl.Type.OutType, basicBlock, varDecl.Name.Name);
			_valueMap[varDecl.Type.OutType] = varPtr;

			// check for initializer and try to evaluate expr
			if (varDecl.Initializer != null)
			{
				var x = GenerateExpressionCode(varDecl.Initializer, basicBlock);
				_builder.BuildStore(x, varPtr);
			}
		}
	}
}
