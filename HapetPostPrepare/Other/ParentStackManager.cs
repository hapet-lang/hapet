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
        private List<(string, GenericType)?> _currentGenericIdMappings = new List<(string, GenericType)?>();

        public ReadOnlyCollection<(string, GenericType)?> CurrentGenericIdMappings => new ReadOnlyCollection<(string, GenericType)?>(_currentGenericIdMappings);

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

            if (parent.HasGenericTypes || parent is AstGenericDecl)
                AddParentGenerics(parent);
        }

        public void RemoveParent()
        {
            var poped = _parentStack.Pop();

            if (poped.HasGenericTypes || poped is AstGenericDecl)
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
            // check if parent is generic decl itself
            if (parent is AstGenericDecl genDecl)
            {
                _currentGenericIdMappings.Add((genDecl.Name.Name, genDecl.Type.OutType as GenericType));
                return;
            }

            // getting pure generics from decl
            var typeGenerics = GenericsHelper.GetGenericsFromName(parent.Name as AstIdGenericExpr, _messageHandler);
            var pureGenerics = GenericsHelper.ExtractAllGenericTypes(typeGenerics.Select(x => x as AstExpression).ToList());
            foreach (var p in pureGenerics)
            {
                _currentGenericIdMappings.Add((p.Name, p.OutType as GenericType));
            }
        }

        private void RemoveParentGenerics(AstDeclaration parent)
        {
            // check if parent is generic decl itself
            if (parent is AstGenericDecl genDecl)
            {
                var theElement = _currentGenericIdMappings.LastOrDefault(x => x.Value.Item1 == genDecl.Name.Name);
                _currentGenericIdMappings.Remove(theElement);
                return;
            }

            // getting pure generics from decl
            var typeGenerics = GenericsHelper.GetGenericsFromName(parent.Name as AstIdGenericExpr, _messageHandler);
            var pureGenerics = GenericsHelper.ExtractAllGenericTypes(typeGenerics.Select(x => x as AstExpression).ToList());
            foreach (var p in pureGenerics)
            {
                var theElement = _currentGenericIdMappings.LastOrDefault(x => x.Value.Item1 == p.Name);
                _currentGenericIdMappings.Remove(theElement);
            }
        }
    }
}
