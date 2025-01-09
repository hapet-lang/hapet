using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Entities;
using HapetFrontend.Types;
using Newtonsoft.Json;
using System;

namespace HapetFrontend.Parsing.PostPrepare
{
    public partial class PostPrepare
    {
        public List<AstClassDecl> AllClassesMetadata { get; } = new List<AstClassDecl>();
		public List<AstStructDecl> AllStructsMetadata { get; } = new List<AstStructDecl>();
		public List<AstEnumDecl> AllEnumsMetadata { get; } = new List<AstEnumDecl>();
		public List<AstDelegateDecl> AllDelegatesMetadata { get; } = new List<AstDelegateDecl>();
		public List<AstFuncDecl> AllFunctionsMetadata { get; } = new List<AstFuncDecl>();

		private List<AstClassDecl> _serializeClassesMetadata { get; } = new List<AstClassDecl>();
		private List<AstStructDecl> _serializeStructsMetadata { get; } = new List<AstStructDecl>();
		private List<AstEnumDecl> _serializeEnumsMetadata { get; } = new List<AstEnumDecl>();
		private List<AstDelegateDecl> _serializeDelegatesMetadata { get; } = new List<AstDelegateDecl>();
		private List<AstFuncDecl> _serializeFunctionsMetadata { get; } = new List<AstFuncDecl>();

		// TODO: some changes should be done in the file when impl 'using' and class inheritance
		private int PostPrepareMetadata()
        {
			PostPrepareInternalShiteInference();
			PostPrepareMetadataTypes();
            PostPrepareMetadataInheritance();
			PostPrepareMetadataDelegates();
			PostPrepareMetadataFunctions();
			PostPrepareMetadataTypeFieldDecls();
			PostPrepareMetadataTypeInheritedFieldDecls();
            PostPrepareMetadataTypeFieldInits();
            PostPrepareMetadataAttributes();

            // if there were errors while preparing for metafile
			if (_compiler.MessageHandler.HasErrors)
			{
				return (int)CompilerErrors.PostPrepareMetafileError; // post prepare errors
			}

			// creating the file
			PostPrepareMetadataCreate();

			// WARN: removing all properties after saving to file
            // removing them only now because we need them to be presented in metadata
			/// unwrapping props is done in <see cref="PostPrepareClassProperties"/>
			RemoveAllProperties();

			return 0;
        }

		private void PostPrepareInternalShiteInference()
		{
			_compiler.GlobalScope.DefineDeclSymbol(AstStringExpr.StringStruct.Name.Name, AstStringExpr.StringStruct);
			AllStructsMetadata.Add(AstStringExpr.StringStruct);
			_compiler.GlobalScope.DefineDeclSymbol(AstArrayExpr.ArrayStruct.Name.Name, AstArrayExpr.ArrayStruct);
			AllStructsMetadata.Add(AstArrayExpr.ArrayStruct);
		}

		private void PostPrepareMetadataTypes()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
					// just skip allowed statements
					if (stmt is AstUsingStmt)
					{
						continue;
					}

					if (stmt is not AstDeclaration decl)
					{
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, stmt, $"The statement expected to be a declaration");
						continue;
					}

					string newName;
                    if (decl is AstClassDecl classDecl)
                    {
						_currentClass = classDecl;

						// creating a new class name with namespace
						newName = $"{file.Namespace}.{classDecl.Name.Name}";
						AllClassesMetadata.Add(classDecl);
						_serializeClassesMetadata.Add(classDecl);
					}
                    else if (decl is AstStructDecl structDecl)
                    {
						// creating a new struct name with namespace
						newName = $"{file.Namespace}.{structDecl.Name.Name}";
						AllStructsMetadata.Add(structDecl);
						_serializeStructsMetadata.Add(structDecl);
					}
					else if (decl is AstEnumDecl enumDecl)
					{
						// creating a new enum name with namespace
						newName = $"{file.Namespace}.{enumDecl.Name.Name}";
						AllEnumsMetadata.Add(enumDecl);
						_serializeEnumsMetadata.Add(enumDecl);
					}
					else if (decl is AstDelegateDecl delegateDecl)
					{
						// creating a new delegate name with namespace
						newName = $"{file.Namespace}.{delegateDecl.Name.Name}";
						AllDelegatesMetadata.Add(delegateDecl);
						_serializeDelegatesMetadata.Add(delegateDecl);
					}
					else
					{
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, $"The declaration type is not allowed in namespace scope");
						continue;
					}

					// TODO: check for partial :)
					decl.Name = decl.Name.GetCopy(newName);
					var smbl = file.NamespaceScope.GetSymbol(decl.Name.Name);
					// TODO: better error like where is the first decl?
					if (smbl != null)
						_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Name, $"Namespace '{file.Namespace}' already contains declaration with the name");
					else
						file.NamespaceScope.DefineDeclSymbol(decl.Name.Name, decl);
				}
            }
        }

        private void PostPrepareMetadataInheritance()
        {
            // resolve inheritance shite of classes
            foreach (var cls in AllClassesMetadata)
            {
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				foreach (var inh in cls.InheritedFrom)
				{
					PostPrepareExprInference(inh);
				}
			}
			// resolve inheritance shite of enums
			foreach (var enm in AllEnumsMetadata)
            {
				_currentSourceFile = enm.SourceFile;
				if (enm.InheritedType == null)
					continue;
				PostPrepareExprInference(enm.InheritedType);
				// only int type inheritance allowed for enums
				if (enm.InheritedType.OutType is not IntType)
				{
					_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, enm.InheritedType, "The type has to be integer type");
				}
			}
        }

		/// <summary>
		/// We need to infer all decl at first and only then - their intializers
		/// </summary>
		private void PostPrepareMetadataTypeFieldDecls()
		{
			void InternalVarPP(AstVarDecl decl)
			{
				PostPrepareExprInference(decl.Type);

				if (decl.Type.OutType is ClassType)
				{
					// the var is actually a pointer to the class
					var astPtr = new AstPointerExpr(decl.Type, false, decl.Type.Location);
					astPtr.Scope = decl.Type.Scope;
					decl.Type = astPtr;
					PostPrepareExprInference(decl.Type);
				}
			}

			// resolve all fields of classes
			foreach (var cls in AllClassesMetadata)
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				// infer fields and props at first
				foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
				{
					// field or property
					InternalVarPP(decl);
				}
			}
			// resolve all fields of structs
			foreach (var str in AllStructsMetadata)
			{
				_currentSourceFile = str.SourceFile;
				// infer fields at first
				foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
				{
					// field 
					InternalVarPP(decl);
				}
			}
			foreach (var enm in AllEnumsMetadata)
			{
				_currentSourceFile = enm.SourceFile;
				// infer fields at first
				foreach (var decl in enm.Declarations)
				{
					// field 
					InternalVarPP(decl);
				}
			}
		}

		private void PostPrepareMetadataTypeInheritedFieldDecls()
		{
			static void CopyInheritedFields(AstClassDecl decl)
			{
				if (decl.InheritedFieldsCopied)
					return;

				// we need to save them into a separate list 
				// to save their inheritance order
				List<AstDeclaration> inheritedFieldDecls = new List<AstDeclaration>();

				// all over the inherited shite
				foreach (var inh in decl.InheritedFrom)
				{
					var inhDecl = (inh.OutType as ClassType).Declaration;
					CopyInheritedFields(inhDecl);

					// all over the parent fields - copy them
					foreach (var fieldDecl in inhDecl.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
					{
						// skip props
						if (fieldDecl is AstPropertyDecl)
							continue;

						// skip consts/statics
						if (fieldDecl.SpecialKeys.Contains(TokenType.KwStatic) || fieldDecl.SpecialKeys.Contains(TokenType.KwConst))
							continue;

						// change parent and scope
						var newVar = fieldDecl.GetCopyForAnotherClass(decl);
                        inheritedFieldDecls.Add(newVar);
						// define the symbol
                        decl.SubScope.DefineDeclSymbol(newVar.Name.Name, newVar);
                    }
                }

				// insert them at the beginning
				decl.Declarations.InsertRange(0, inheritedFieldDecls);

				decl.InheritedFieldsCopied = true;
            }

			// resolve all inherited fields of classes
			foreach (var cls in AllClassesMetadata)
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;

				CopyInheritedFields(cls);
            }
		}

        private void PostPrepareMetadataTypeFieldInits()
        {
			// resolve all fields of classes
			foreach (var cls in AllClassesMetadata)
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				// infer fields and props at first
				foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
				{
					// field or property
					PostPrepareVarInference(decl, true);
				}
			}
			// resolve all fields of structs
			foreach (var str in AllStructsMetadata)
			{
				_currentSourceFile = str.SourceFile;
				// infer fields at first
				foreach (var decl in str.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
				{
					// field 
					PostPrepareVarInference(decl);
				}
			}
			foreach (var enm in AllEnumsMetadata)
			{
				_currentSourceFile = enm.SourceFile;
				// generating all the values of fields
				int currentValue = 0;
				List<int> allValues = new List<int>(enm.Declarations.Count);

				// infer fields at first
				foreach (var decl in enm.Declarations)
				{
					// field 
					PostPrepareVarInference(decl);
					// this shite is to generate values for enum fields
					if (decl.Initializer == null)
					{
						decl.Initializer = PostPrepareExpressionWithType(decl.Type.OutType, new AstNumberExpr(NumberData.FromInt(currentValue)));
						// warn if the value already exists in enum
						if (allValues.Contains(currentValue))
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, "Enum field with the same value already exists", null, ReportType.Warning);
						}
						allValues.Add(currentValue);
						currentValue++;
					}
					else
					{
						if (decl.Initializer.OutValue == null)
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, "The initializer has to be compile time evaluated!");
							continue;
						}
						else if (decl.Initializer.OutValue is not NumberData)
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl.Initializer, "The initializer has to be numeric type");
							continue;
						}
						var userDefinedValue = (int)((NumberData)decl.Initializer.OutValue).IntValue;
						// warn if the value already exists in enum
						if (allValues.Contains(userDefinedValue))
						{
							_compiler.MessageHandler.ReportMessage(_currentSourceFile.Text, decl, "Enum field with the same value already exists", null, ReportType.Warning);
						}
						allValues.Add(userDefinedValue);
						currentValue = userDefinedValue + 1; // getting value for the next field
					}
				}
			}
        }

		private void PostPrepareMetadataDelegates()
		{
			// inferrencing delegates
			foreach (var del in AllDelegatesMetadata)
			{
				_currentSourceFile = del.SourceFile;
				PostPrepareDelegateInference(del);
			}
		}

        private void PostPrepareMetadataFunctions()
        {
			// inferrencing funcs
			// WARN! _serializeClassesMetadata is used because we don't won't external funcs to be inferred like that
			foreach (var cls in _serializeClassesMetadata) 
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
				{
					PostPrepareFunctionInference(decl, true);
					AllFunctionsMetadata.Add(decl);
					_serializeFunctionsMetadata.Add(decl);
				}
			}
        }

        private void PostPrepareMetadataAttributes()
        {
			// inferrencing attribtues of functions
            foreach (var fnc in AllFunctionsMetadata)
            {
				_currentSourceFile = fnc.SourceFile;
				// inferencing attrs
				foreach (var a in fnc.Attributes)
				{
					PostPrepareExprInference(a);
				}
				// inferencing params attrs
				foreach (var p in fnc.Parameters)
				{
					// inferencing attrs
					foreach (var a in p.Attributes)
					{
						PostPrepareExprInference(a);
					}
				}
			}
			// inferrencing attribtues of classes
			foreach (var cls in AllClassesMetadata)
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				// infer fields and props attibutes
				foreach (var decl in cls.Declarations.Where(x => x is AstVarDecl).Select(x => x as AstVarDecl))
				{
					// inferencing attrs
					foreach (var a in decl.Attributes)
					{
						PostPrepareExprInference(a);
					}
				}
				// inferencing attrs
				foreach (var a in cls.Attributes)
				{
					PostPrepareExprInference(a);
				}
			}
			// inferrencing attribtues of structs
			foreach (var str in AllStructsMetadata)
			{
				_currentSourceFile = str.SourceFile;
				// inferencing attrs
				foreach (var a in str.Attributes)
				{
					PostPrepareExprInference(a);
				}
			}
			// inferrencing attribtues of enums
			foreach (var enm in AllEnumsMetadata)
			{
				_currentSourceFile = enm.SourceFile;
				// inferencing attrs
				foreach (var a in enm.Attributes)
				{
					PostPrepareExprInference(a);
				}
			}
			// inferrencing attribtues of delegates
			foreach (var del in AllDelegatesMetadata)
			{
				_currentSourceFile = del.SourceFile;
				// inferencing attrs
				foreach (var a in del.Attributes)
				{
					PostPrepareExprInference(a);
				}
				// inferencing params attrs
				foreach (var p in del.Parameters)
				{
					// inferencing attrs
					foreach (var a in p.Attributes)
					{
						PostPrepareExprInference(a);
					}
				}
			}
		}

        private void PostPrepareMetadataCreate()
        {
			var projectVersion= _compiler.CurrentProjectSettings.ProjectVersion;

			// TODO: probably should be sorted somehow by inheritance, idk
			MetadataJson metadata = new MetadataJson();
            metadata.Version = projectVersion;
            // serialize all unreflected
            metadata.ClassDecls = _serializeClassesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.StructDecls = _serializeStructsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.EnumDecls = _serializeEnumsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.DelegateDecls = _serializeDelegatesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.FuncDecls = _serializeFunctionsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();

            // WARN: take care about the shite that is goin on here
            var sz = JsonConvert.SerializeObject(metadata, Formatting.Indented);
            var outFolderPath = _compiler.CurrentProjectSettings.OutputDirectory;
            var projectName = _compiler.CurrentProjectSettings.ProjectName;
            File.WriteAllText($"{outFolderPath}/{projectName}.json", sz);
		}

        private void RemoveAllProperties()
        {
            foreach (var cls in AllClassesMetadata)
            {
                cls.Declarations.RemoveAll(x => x is AstPropertyDecl);
            }
        }
    }

    public class MetadataJson
    {
        public string Version { get; set; }
        public List<ClassDeclJson> ClassDecls { get; set; }
        public List<StructDeclJson> StructDecls { get; set; }
        public List<EnumDeclJson> EnumDecls { get; set; }
        public List<DelegateDeclJson> DelegateDecls { get; set; }
        public List<FuncDeclJson> FuncDecls { get; set; }
    }
}
