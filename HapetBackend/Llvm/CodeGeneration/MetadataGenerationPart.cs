using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Parsing;
using HapetFrontend.Types;
using LLVMSharp.Interop;
using System;
using System.Xml.Linq;

namespace HapetBackend.Llvm
{
	public partial class LlvmCodeGenerator
	{
		private readonly Dictionary<ClassType, LLVMValueRef> _typeInfoDictionary = new Dictionary<ClassType, LLVMValueRef>();

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
				_currentSourceFile = cls.SourceFile;
				// doing that we are registering the type in dict
				var _ = HapetTypeToLLVMType(cls.Type.OutType);

				// reg type info
				_typeInfoDictionary.Add(cls.Type.OutType as ClassType, GenerateTypeInfoConst(cls.Type.OutType as ClassType));
            }
			// all over the structs
			foreach (var str in _postPreparer.AllStructsMetadata)
			{
                _currentSourceFile = str.SourceFile;
                // doing that we are registering the type in dict
                var _ = HapetTypeToLLVMType(str.Type.OutType);
			}
			// all over the enums
			foreach (var enm in _postPreparer.AllEnumsMetadata)
			{
				_currentSourceFile = enm.SourceFile;
				// doing that we are registering the type in dict
				var _ = HapetTypeToLLVMType(enm.Type.OutType);
			}
		}

		// TODO: mb refactor somehow?
		private void GenerateMetadataShiteFields()
		{
			foreach (var cls in _postPreparer.AllClassesMetadata)
			{
                _currentSourceFile = cls.SourceFile;

                var classStruct = HapetTypeToLLVMType(cls.Type.OutType);

				var entryTypes = new List<LLVMTypeRef>();
				var entryHapetTypes = new List<HapetType>();

				// TODO: entry for type info
				entryTypes.Add(_context.Int8Type.GetPointerTo());
				entryHapetTypes.Add(PointerType.GetPointerType(IntPtrType.Instance));

				// get all fields from base classes/interfaces
				foreach (var bs in cls.InheritedFrom)
				{
					if (bs.OutType is not ClassType clsType)
						continue;
					// get all fields that are not static and const
					var bFields = cls.Declarations
						.Where(x => (x is AstVarDecl && x is not AstPropertyDecl) && 
							(!x.SpecialKeys.Contains(TokenType.KwStatic) && 
							!x.SpecialKeys.Contains(TokenType.KwConst)))
						.Select(x => x as AstVarDecl);
					foreach (var f in bFields)
					{
                        // if it is non const/static - create a field in struct
                        entryTypes.Add(HapetTypeToLLVMType(f.Type.OutType));
                        entryHapetTypes.Add(f.Type.OutType);
                    }
                }

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
						else
						{
							// set default value to it
							globStatic.Initializer = GenerateExpressionCode(AstDefaultExpr.GetDefaultValueForType(decl.Type.OutType, null));
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
			foreach (var enm in _postPreparer.AllEnumsMetadata)
			{
				foreach (var decl in enm.Declarations)
				{
					// creating a static field of the enum
					var globStatic = _module.AddGlobal(HapetTypeToLLVMType(decl.Type.OutType), $"{enm.Type.OutType}::{decl.Name.Name}");
					if (decl.Initializer == null)
						_messageHandler.ReportMessage(_currentSourceFile.Text, decl, $"Enum field initializer could not be null");
					globStatic.Initializer = GenerateExpressionCode(decl.Initializer);
					_valueMap[decl.GetSymbol] = globStatic;
				}
			}
			// TODO: structs and other shite
		}

		private void GenerateMetadataShiteFuncs()
		{
			foreach (var func in _postPreparer.AllFunctionsMetadata)
			{
                _currentSourceFile = func.SourceFile;
                GenerateFuncCode(func, null, true);
			}
		}

        #region Additional shite for classes
		private unsafe LLVMValueRef GenerateTypeInfoConst(ClassType cls)
		{
			if (_typeInfoDictionary.ContainsKey(cls))
				return _typeInfoDictionary[cls];

			// create if does not exists
			ClassType parent = cls.Declaration.InheritedFrom.FirstOrDefault(x => x.OutType is ClassType)?.OutType as ClassType;
			LLVMTypeRef typeInfoType = GetTypeInfoType();
			// idk wtf is that shite :)
			var ptrT = LLVMTypeRef.CreatePointer(_context.Int8Type, 0);
            var nullPtr = LLVMValueRef.CreateConstPointerNull(ptrT);
            LLVMValueRef parentRef = parent == null ? 
					(LLVMValueRef)LLVMValueRef.CreateConstStruct(new LLVMValueRef[] { LLVMValueRef.CreateConstPointerCast(nullPtr, ptrT) }, false) : 
				GenerateTypeInfoConst(parent);

            var globConst = _module.AddGlobal(typeInfoType, $"TypeInfo::{cls.Declaration.Name.Name}");
            globConst.Initializer = LLVMValueRef.CreateConstStruct(new LLVMValueRef[] { parentRef }, false);
            globConst.Linkage = (LLVMLinkage.LLVMInternalLinkage);


            // Отладочная информация
            Console.WriteLine("Структура type.info: " + typeInfoType.PrintToString());
            Console.WriteLine("Тип nullPointer: " + nullPtr.TypeOf.PrintToString());
            Console.WriteLine("Тип инициализатора: " + parentRef.TypeOf.PrintToString());
            Console.WriteLine("Тип глобальной переменной: " + globConst.TypeOf.PrintToString());
            return globConst;
        }

		private LLVMTypeRef _typeInfoType;
        private LLVMTypeRef GetTypeInfoType()
		{
			if (_typeInfoType != null)
				return _typeInfoType;

            _typeInfoType = _context.CreateNamedStruct($"type.info");
            var parent = LLVMTypeRef.CreatePointer(_context.Int8Type, 0); // parent
            _typeInfoType.StructSetBody(new LLVMTypeRef[] { parent }, false);
			return _typeInfoType;
        }
        #endregion
    }
}
