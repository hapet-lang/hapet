using HapetFrontend.Ast.Declarations;
using HapetFrontend.Types;
using LLVMSharp.Interop;

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
				foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl && x is not AstPropertyDecl))
				{
					var fieldDecl = decl as AstVarDecl;
					entryTypes.Add(HapetTypeToLLVMType(fieldDecl.Type.OutType));
					entryHapetTypes.Add(fieldDecl.Type.OutType);
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
