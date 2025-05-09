using System.Collections.Immutable;
using System.Collections.ObjectModel;
using HapetFrontend;
using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Helpers;
using HapetFrontend.Types;

namespace HapetPostPrepare.Other
{
    public class ParentStackManager
    {
        private IMessageHandler _messageHandler;
        private Stack<AstDeclaration> _parentStack { get; } = new Stack<AstDeclaration>();

        /// <summary>
        /// The dict is going to be used as a holder
        /// for current preparing generic types. So <see cref="PostPrepareIdentifierInference"/>
        /// would return correct types for ids
        /// </summary>
        private Dictionary<string, GenericType> _currentGenericIdMappings = new Dictionary<string, GenericType>();

        public ReadOnlyDictionary<string, GenericType> CurrentGenericIdMappings => new ReadOnlyDictionary<string, GenericType>(_currentGenericIdMappings);

        private ParentStackManager() { }
        public static ParentStackManager Create(IMessageHandler messageHandler)
        {
            return new ParentStackManager()
            {
                _messageHandler = messageHandler
            };
        }

        public void AddParent(AstDeclaration parent)
        {
            _parentStack.Push(parent);

            if (parent.HasGenericTypes)
                AddParentGenerics(parent);
        }

        public void RemoveParent()
        {
            var poped = _parentStack.Pop();

            if (poped.HasGenericTypes)
                RemoveParentGenerics(poped);
        }

        public AstDeclaration GetNearestParentClassOrStruct()
        {
            foreach (var p in _parentStack.AsEnumerable())
            {
                if (p is AstClassDecl || p is AstStructDecl)
                    return p;
            }
            return null;
        }

        public AstFuncDecl GetNearestParentFunc()
        {
            foreach (var p in _parentStack.AsEnumerable())
            {
                if (p is AstFuncDecl func)
                    return func;
            }
            return null;
        }

        private void AddParentGenerics(AstDeclaration parent)
        {
            // getting pure generics from decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(parent.Name as AstIdGenericExpr, _messageHandler);
            foreach (var p in pureGenerics)
            {
                _currentGenericIdMappings.Add(p.Name, p.OutType as GenericType);
            }
        }

        private void RemoveParentGenerics(AstDeclaration parent)
        {
            // getting pure generics from decl
            var pureGenerics = GenericsHelper.GetGenericsFromName(parent.Name as AstIdGenericExpr, _messageHandler);
            foreach (var p in pureGenerics)
            {
                _currentGenericIdMappings.Remove(p.Name);
            }
        }
    }
}
