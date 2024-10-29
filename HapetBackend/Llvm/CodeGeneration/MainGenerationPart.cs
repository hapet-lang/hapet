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
                    entryTypes.Add(HapetTypeToLLVMType(fieldDecl.Type.OutType));
					entryHapetTypes.Add(fieldDecl.Type.OutType);
                }
			}

			// TODO: create using HapetTypeToLLVMType
			_structTypeElementsMap.Add(classDecl.Type.OutType, entryHapetTypes);
            classStruct.StructSetBody(entryTypes.ToArray(), false);

			foreach (var (funcDecl, funcType) in funcs)
			{
				GenerateFuncCode(funcDecl, funcType, classDecl);
			}
		}

		private LLVMValueRef _lastFunctionValueRef = default;
		private unsafe void GenerateFuncCode(AstFuncDecl funcDecl, LLVMTypeRef? funcType = null, AstClassDecl classDecl = null)
		{
			_currentFunction = funcDecl;

			funcType ??= HapetTypeToLLVMType(funcDecl.Type.OutType);

			string funcName = funcDecl.Name.Name;

			// declaring global func
			LLVMValueRef lfunc = _module.AddFunction(funcName, funcType.Value);
			lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;
			lfunc.DLLStorageClass = LLVMDLLStorageClass.LLVMDLLExportStorageClass;

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
				// var parArg = _builder.BuildLoad2(HapetTypeToLLVMType(p.Type.OutType), addrAlloca, $"{p.Name.Name}1");
				// _refMap[p.GetSymbol] = addrAlloca;
				// _valueMap[p.GetSymbol] = parArg;
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
				_errorHandler.ReportError(_currentSourceFile.Text, funcDecl, $"'DllImportAttribute' has to be specified when function is 'extern'");
				return;
			}
			if (dllImportAttr.Parameters.Count < 2)
			{
				_errorHandler.ReportError(_currentSourceFile.Text, dllImportAttr, $"Both 'DllName' and 'EntryPoint' has to be specified");
				return;
			}
			// TODO: the types has to be checked in type inference
			if (dllImportAttr.Parameters[0].OutValue is not string)
			{
				_errorHandler.ReportError(_currentSourceFile.Text, dllImportAttr.Parameters[0], $"Out type of the expr has to be string");
				return;
			}
			if (dllImportAttr.Parameters[1].OutValue is not string)
			{
				_errorHandler.ReportError(_currentSourceFile.Text, dllImportAttr.Parameters[1], $"Out type of the expr has to be string");
				return;
			}
			string dllName = dllImportAttr.Parameters[0].OutValue as string; 
			string entryPoint = dllImportAttr.Parameters[1].OutValue as string; 

			// check if import from staticly linked shite
			if (string.IsNullOrWhiteSpace(dllName))
			{
				// the same type
				var funcType = HapetTypeToLLVMType(funcDecl.Type.OutType);

				// declaring external global func
				LLVMValueRef lfunc = _module.AddFunction(entryPoint, funcType);
				lfunc.Linkage = LLVMLinkage.LLVMExternalLinkage;

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
				// TODO: import from DLL
			}
		}
	}
}
