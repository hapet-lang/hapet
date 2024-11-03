using HapetFrontend.Ast.Declarations;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		/// <summary>
		/// Inits some dicts and other shite with metadata types :)
		/// </summary>
		private void GenerateMetadataShite()
		{
			GenerateMetadataShiteTypes();
			GenerateMetadataShiteFuncs();
			GenerateMetadataShiteFields();
		}

		// TODO: mb refactor somehow?
		private void GenerateMetadataShiteTypes() 
		{
			// generate default types at first just for fun
			HapetTypeToLLVMType(ArrayType.GetArrayType(VoidType.Instance));
			HapetTypeToLLVMType(StringType.Instance);

			// all over the classes
			foreach (var cls in _postPreparer.AllClassesMetadata)
			{
				// doing that we are registering the type in dict
				var _ = HapetTypeToLLVMType(cls.Type.OutType);
			}
			// all over the structs
			foreach (var str in _postPreparer.AllStructsMetadata)
			{
				// doing that we are registering the type in dict
				var _ = HapetTypeToLLVMType(str.Type.OutType);
			}
		}

		// TODO: mb refactor somehow?
		private void GenerateMetadataShiteFields()
		{
			foreach (var cls in _postPreparer.AllClassesMetadata)
			{
				var classStruct = HapetTypeToLLVMType(cls.Type.OutType);

				var entryTypes = new List<LLVMTypeRef>();
				var entryHapetTypes = new List<HapetType>();

				// TODO: entry for type info
				entryTypes.Add(_context.Int8Type.GetPointerTo());
				entryHapetTypes.Add(PointerType.GetPointerType(IntType.GetIntType(1, false)));

				// getting all field except props
				foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl).Select(x => x as AstVarDecl))
				{
					// check for const/static fields
					if (decl.SpecialKeys.Contains(TokenType.KwStatic))
					{
						// creating a static field of the class
						var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{cls.Type.OutType}::{decl.Name.Name}");
						if (decl.Initializer != null)
						{
							globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
						}
                        _valueMap[decl.GetSymbol] = globStatic;
                    }
					else if (decl.SpecialKeys.Contains(TokenType.KwConst))
					{
						// creating a const field of the class
						// TODO: consts should not create a variable in LLVM IR 
						// just use their values where needed
						var globConst = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{cls.Type.OutType}::{decl.Name.Name}");
						globConst.Initializer = HapetValueToLLVMValue(decl.Type.OutType, decl.Initializer.OutValue);
                        _valueMap[decl.GetSymbol] = globConst;
                    }
					else
					{
						// if it is non const/static - create a field in struct
						entryTypes.Add(HapetTypeToLLVMType(decl.Type.OutType));
						entryHapetTypes.Add(decl.Type.OutType);
					}
				}

				_structTypeElementsMap.Add(cls.Type.OutType, entryHapetTypes);
				classStruct.StructSetBody(entryTypes.ToArray(), false);
			}
			// TODO: structs and other shite
		}

		private void GenerateMetadataShiteFuncs()
		{
			foreach (var func in _postPreparer.AllFunctionsMetadata)
			{
				GenerateFuncCode(func, null, true);
			}
		}
	}
}
