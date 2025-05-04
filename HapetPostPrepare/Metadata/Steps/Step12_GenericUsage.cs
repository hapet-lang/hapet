using HapetFrontend.Ast;
using HapetFrontend.Types;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        /// <summary>
        /// In <see cref="PostPrepareGenericType"/> the dict is going to be used as a holder
        /// for current preparing generic types. So <see cref="PostPrepareIdentifierInference"/>
        /// would return correct types for ids
        /// </summary>
        private Dictionary<string, GenericType> _currentGenericIdMappings = new Dictionary<string, GenericType>();

        public void PostPrepareGenericType(AstStatement decl)
        {
            // TODO: post prepare all insides
            // check that all callings on generic types are normal
            // and some shite like a + b allowed and etc.
        }
    }
}
