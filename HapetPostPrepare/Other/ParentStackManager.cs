using System.Collections.Immutable;
using HapetFrontend.Ast;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        private Stack<AstDeclaration> ParentStack { get; } = new Stack<AstDeclaration>();

        /// <summary>
        /// The dict is going to be used as a holder
        /// for current preparing generic types. So <see cref="PostPrepareIdentifierInference"/>
        /// would return correct types for ids
        /// </summary>
        private Dictionary<string, GenericType> _currentGenericIdMappings = new Dictionary<string, GenericType>();

        private void AddParent(AstDeclaration parent)
        {
            ParentStack.Push(parent);

            if (parent.HasGenericTypes)
                _AddParentGenerics(parent);
        }

        private void RemoveParent()
        {
            var poped = ParentStack.Pop();

            if (poped.HasGenericTypes)
                _RemoveParentGenerics(poped);
        }

        private void _AddParentGenerics(AstDeclaration parent)
        {
            foreach (var p in parent.GenericNames)
            {
                _currentGenericIdMappings.Add(p.Name, p.OutType as GenericType);
            }
        }

        private void _RemoveParentGenerics(AstDeclaration parent)
        {
            foreach (var p in parent.GenericNames)
            {
                _currentGenericIdMappings.Remove(p.Name);
            }
        }
    }
}
