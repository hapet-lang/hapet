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
		private AstFuncDecl _currentFunction;

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
			var funcs = new Dictionary<AstFuncDecl, LLVMTypeRef>();
			foreach (var decl in classDecl.Declarations)
			{
				if (decl is AstFuncDecl funcDecl)
				{
					// defining global func
					var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);
					funcs.Add(funcDecl, funcType);
				}
			}

			foreach (var (funcDecl, funcType) in funcs)
			{
				GenerateFuncCode(funcDecl, funcType);
			}
		}

		private LLVMValueRef _lastFunctionValueRef = default;
		private unsafe void GenerateFuncCode(AstFuncDecl funcDecl, LLVMTypeRef? funcType = null, bool forMetadata = false)
		{
			_currentFunction = funcDecl;

			funcType ??= HapetTypeToLLVMType(funcDecl.Type.OutType);

			string funcName = funcDecl.Name.Name;

			// if it is for metadata - only 'declares' would be generated that would be replaced with 'defines' in the future
			if (forMetadata)
			{
				// declaring global func
				LLVMValueRef lfunc = _module.AddFunction(funcName, funcType.Value);
				// make the function dllexport when it is not 'unreflected'
				if (!funcDecl.SpecialKeys.Contains(TokenType.KwUnreflected))
				{
					lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
					lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;
					
					// for win-x86 callconv is that
					if (_compiler.CurrentProjectSettings.TargetPlatformData.TargetPlatform == HapetFrontend.TargetPlatform.Win86)
					{
						lfunc.FunctionCallConv = (uint)LLVMCallConv.LLVMCCallConv; // cdecl
					}
				}
				else
				{
					lfunc.Linkage = LLVMLinkage.LLVMInternalLinkage;
				}

				// caching the function											 
				_valueMap[funcDecl.GetSymbol] = lfunc;
				_lastFunctionValueRef = lfunc;

				// setting parameter names
				for (int i = 0; i < funcDecl.Parameters.Count; ++i)
				{
					var p = funcDecl.Parameters[i];
					if (p.Name != null)
						lfunc.Params[i].Name = p.Name.Name;
				}
			}
			else
			{
				// getting the func
				LLVMValueRef lfunc = _valueMap[funcDecl.GetSymbol];
				_lastFunctionValueRef = lfunc;

				// check if there is no implementation and it is not an extern shite
				if (funcDecl.Body == null && !funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
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
					_valueMap[p.GetSymbol] = addrAlloca;
				}

				// function body
				var bbBody = lfunc.AppendBasicBlock("entry");
				_builder.BuildBr(bbBody);
				_builder.PositionAtEnd(bbBody);

				// different behaviour when extern func
				if (funcDecl.SpecialKeys.Contains(TokenType.KwExtern))
				{
					// when extern func
					GenerateExternFunctionBody(funcDecl);
				}
				else
				{
					// genereting inside stuff of the function
					GenerateBlockExprCode(funcDecl.Body);
				}

				lfunc.VerifyFunction(LLVMVerifierFailureAction.LLVMPrintMessageAction);
			}			
		}

		private void GenerateVarDeclCode(AstVarDecl varDecl)
		{
			// alloca new var in basicBlock
			var varPtr = CreateLocalVariable(varDecl.Type.OutType, varDecl.Name.Name);

			// check for initializer and try to evaluate expr
			if (varDecl.Initializer != null)
			{
				AssignToVar(varPtr, varDecl.Type.OutType, varDecl.Initializer);
			}

			// _refMap[varDecl.GetSymbol] = varPtr;
			// _valueMap[varDecl.GetSymbol] = _builder.BuildLoad2(HapetTypeToLLVMType(varDecl.Type.OutType), varPtr, varDecl.Name.Name);
			_valueMap[varDecl.GetSymbol] = varPtr;
		}

		private void GenerateExternFunctionBody(AstFuncDecl funcDecl)
		{
			// creating extern call of C func
			string dllImportAttrFullName = "System.Runtime.InteropServices.DllImportAttribute"; // WARN: hard cock
			var dllImportAttr = funcDecl.Attributes.FirstOrDefault(x => x.AttributeName.OutType.ToString() == dllImportAttrFullName);

			// many checks are here
			if (dllImportAttr == null) 
			{
				_messageHandler.ReportMessage(_currentSourceFile.Text, funcDecl, $"'DllImportAttribute' has to be specified when function is 'extern'");
				return;
			}
			string dllName = dllImportAttr.Parameters[0].OutValue as string; 
			string entryPoint = dllImportAttr.Parameters[1].OutValue as string;

			LLVMTypeRef funcType;
			LLVMValueRef funcValue;

			// check if import from staticly linked shite
			if (string.IsNullOrWhiteSpace(dllName))
			{
				// the same type
				funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);

				// declaring external global func
				funcValue = _module.AddFunction(entryPoint, funcType);
				funcValue.Linkage = LLVMLinkage.LLVMExternalLinkage;
				funcValue.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLImportStorageClass;

				// setting parameter names
				for (int i = 0; i < funcDecl.Parameters.Count; ++i)
				{
					var p = funcDecl.Parameters[i];
					if (p.Name != null)
						funcValue.Params[i].Name = p.Name.Name;
				}
			}
			else
			{
				funcType = null;
				funcValue = null;
				// TODO: import from DLL
			}

			// generating params
			List<LLVMValueRef> parameters = new List<LLVMValueRef>();
			for (int i = 0; i < funcDecl.Parameters.Count; ++i)
			{
				var p = funcDecl.Parameters[i];
				var vptr = _valueMap[p.GetSymbol];
				var loaded = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), vptr, p.Name.Name);
				parameters.Add(loaded);
			}

			// if there is smth to return
			if (funcDecl.Returns.OutType is VoidType)
			{
				_builder.BuildCall2(funcType, funcValue, parameters.ToArray());
				_builder.BuildRetVoid();
			}
			else
			{
				var v = _builder.BuildCall2(funcType, funcValue, parameters.ToArray(), $"{entryPoint}Result");
				_builder.BuildRet(v);
			}
		}
	}
}
