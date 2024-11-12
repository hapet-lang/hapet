using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
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

        // TODO: some changes should be done in the file when impl 'using' and class inheritance

        private int PostPrepareMetadata()
        {
            PostPrepareMetadataTypes();
            PostPrepareMetadataInheritance();
            PostPrepareMetadataFunctions();
            PostPrepareMetadataTypeFields();
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

        private void PostPrepareMetadataTypes()
        {
            foreach (var (path, file) in _compiler.GetFiles())
            {
                _currentSourceFile = file;
                foreach (var stmt in file.Statements)
                {
                    if (stmt is AstClassDecl classDecl)
                    {
						_currentClass = classDecl;

						// creating a new class name with namespace
						string newClassName = $"{file.Namespace}.{classDecl.Name.Name}";
                        classDecl.Name = classDecl.Name.GetCopy(newClassName);
                        file.NamespaceScope.DefineDeclSymbol(classDecl.Name.Name, classDecl);
						AllClassesMetadata.Add(classDecl);
                    }
                    else if (stmt is AstStructDecl structDecl)
                    {
                        // creating a new struct name with namespace
                        string newClassName = $"{file.Namespace}.{structDecl.Name.Name}";
                        structDecl.Name = structDecl.Name.GetCopy(newClassName);
                        file.NamespaceScope.DefineDeclSymbol(structDecl.Name.Name, structDecl);
						AllStructsMetadata.Add(structDecl);
                    }
					else if (stmt is AstEnumDecl enumDecl)
					{
						// creating a new enum name with namespace
						string newClassName = $"{file.Namespace}.{enumDecl.Name.Name}";
						enumDecl.Name = enumDecl.Name.GetCopy(newClassName);
						file.NamespaceScope.DefineDeclSymbol(enumDecl.Name.Name, enumDecl);
						AllEnumsMetadata.Add(enumDecl);
					}
					else if (stmt is AstDelegateDecl delegateDecl)
					{
						// creating a new delegate name with namespace
						string newClassName = $"{file.Namespace}.{delegateDecl.Name.Name}";
						delegateDecl.Name = delegateDecl.Name.GetCopy(newClassName);
						file.NamespaceScope.DefineDeclSymbol(delegateDecl.Name.Name, delegateDecl);
						AllDelegatesMetadata.Add(delegateDecl);
					}
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

		private void PostPrepareMetadataTypeFields()
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

        private void PostPrepareMetadataFunctions()
        {
			// inferrencing delegates
			foreach (var del in AllDelegatesMetadata)
			{
				_currentSourceFile = del.SourceFile;
				PostPrepareDelegateInference(del);
			}
			// inferrencing funcs
			foreach (var cls in AllClassesMetadata)
			{
				_currentSourceFile = cls.SourceFile;
				_currentClass = cls;
				foreach (var decl in cls.Declarations.Where(x => x is AstFuncDecl).Select(x => x as AstFuncDecl))
				{
					PostPrepareFunctionInference(decl, true);
					AllFunctionsMetadata.Add(decl);
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
            metadata.ClassDecls = AllClassesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.StructDecls = AllStructsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.EnumDecls = AllEnumsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.DelegateDecls = AllDelegatesMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();
            metadata.FuncDecls = AllFunctionsMetadata.Where(x => !x.SpecialKeys.Contains(TokenType.KwUnreflected)).Select(x => x.GetJson()).ToList();

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
