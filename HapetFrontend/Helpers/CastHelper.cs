using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Entities;
using HapetFrontend.Enums;
using HapetFrontend.Errors;
using HapetFrontend.Scoping;
using HapetFrontend.Types;

namespace HapetFrontend
{
    public partial class Compiler
    {
        public AstExpression TryCastExpr(HapetType targetType, AstExpression currentExpr, CastResult castResult = null, ProgramFile sourceFile = null)
        {
            HapetType currentType = currentExpr.OutType;
            AstExpression outExpr = null;

            // assing expr's source file if arg is null
            sourceFile ??= currentExpr.SourceFile;

            // error and return the same expr if outType is null
            if (targetType == null)
            {
                if (castResult == null)
                    MessageHandler.ReportMessage(sourceFile, currentExpr, [], ErrorCode.Get(CTEN.RequiredTypeNotEvaluated));
                return currentExpr;
            }

            // error and return the same expr if currType is null
            if (currentType == null)
            {
                if (castResult == null)
                    MessageHandler.ReportMessage(sourceFile, currentExpr, [], ErrorCode.Get(CTEN.ExprTypeNotEvaluated));
                return currentExpr;
            }

            // if the types are equal - no need to cast anything, so return orig
            if (targetType == currentType)
            {
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return currentExpr;
            }

            // no need for any casts if it is 'var' shite
            // just return back the expr
            if (targetType is VarType)
            {
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return currentExpr;
            }

            // creating a cast expr to return it further
            var targetExpr = new AstEmptyExpr(currentExpr)
            {
                Scope = currentExpr.Scope,
                Parent = currentExpr.Parent,
                SourceFile = currentExpr.SourceFile,
                OutType = targetType,
            };
            var cst = new AstCastExpr(targetExpr, currentExpr, currentExpr.Location);
            cst.OutType = targetType;
            cst.Scope = currentExpr.Scope;
            cst.OutValue = currentExpr.OutValue;

            // check for user defined implicit casts
            var castOps = currentExpr.Scope.GetBinaryOperators("cast", targetType, currentType);
            var implicitOps = castOps.Where(x => x is UserDefinedBinaryOperator userDef &&
                                                 userDef.Function.Declaration is AstOverloadDecl overDecl &&
                                                 overDecl.OverloadType == OverloadType.ImplicitCast).ToList();
            // if there is an implicit cast - return it 
            if (implicitOps.Count == 1)
            {
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return cst;
            }
            else if (implicitOps.Count > 1)
            {
                if (castResult == null)
                    MessageHandler.ReportMessage(sourceFile, currentExpr, 
                        [HapetType.AsString(currentType), HapetType.AsString(targetType)], 
                        ErrorCode.Get(CTEN.AmbiguousCastOverloads));
            }

            switch (targetType)
            {
                // default cringe casting
                case FloatType when currentType is IntType:
                case FloatType when currentType is FloatType:
                case FloatType when currentType is CharType:
                case IntType when currentType is CharType:
                case IntType when currentType is IntType:
                case CharType when currentType is IntType:
                    {
                        // numeric types always could be narrowed
                        if (castResult != null)
                            castResult.CouldBeNarrowed = true;

                        bool? isTargetSigned = targetType switch
                        {
                            FloatType => null,
                            IntType i => i.Signed,
                            CharType => false,
                            _ => null,
                        };
                        bool? isCurrentSigned = currentType switch
                        {
                            FloatType => null,
                            IntType i => i.Signed,
                            CharType => false,
                            _ => null,
                        };

                        // do not allow if signes are different or something like that. idk :)
                        // allow if the var type size is bigger or equal
                        // byte a = 5;
                        // uint b = a;
                        if (targetType.GetSize() >= currentType.GetSize() &&
                            (isTargetSigned != null && isCurrentSigned != null) &&
                            (isTargetSigned.Value == isCurrentSigned.Value))
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                            break;
                        }

                        // allow shite like:
                        // byte a = 5;
                        // int b = a;
                        if (targetType.GetSize() > currentType.GetSize() &&
                            (isTargetSigned != null && isCurrentSigned != null) &&
                            (isTargetSigned.Value && !isCurrentSigned.Value))
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                            break;
                        }

                        // allow to cast all int values to float
                        // int b = 3;
                        // float a = b;
                        if (targetType.GetSize() >= currentType.GetSize() &&
                            targetType is FloatType &&
                            (currentType is IntType || currentType is CharType))
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                            break;
                        }

                        // allow to cast all small floats to bigger floats
                        // float b = 3;
                        // double a = b;
                        if (targetType.GetSize() > currentType.GetSize() &&
                            targetType is FloatType && currentType is FloatType)
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                            break;
                        }

                        // if outValue is null:
                        // there is no way to implicitly cast non-compiletime values
                        if (currentExpr.OutValue == null)
                            break;

                        // it the value is in range of the target - then it could be easily casted :)
                        if (currentExpr.OutValue is char charData)
                        {
                            // getting a NumberData from char UTF-16 value to normally check ranging
                            var newNumData = NumberData.FromInt(((short)charData));
                            if (newNumData.IsInRangeOfType(targetType))
                            {
                                if (castResult != null)
                                    castResult.CouldBeCasted = true;
                                outExpr = cst;
                            }
                        }
                        // it the value is in range of the target - then it could be easily casted :)
                        else if (currentExpr.OutValue is NumberData numData && numData.IsInRangeOfType(targetType))
                        {
                            if (castResult != null)
                                castResult.CouldBeCasted = true;
                            outExpr = cst;
                        }

                        break;
                    }
                
                // ptr to intptr and vise versa
                case PointerType when currentType is IntPtrType:
                case IntPtrType when currentType is PointerType:

                // every ptr type can be casted to void* implicitly like
                // void* anime = ptrToSmth;
                case PointerType ptr5 when ptr5.TargetType == HapetType.CurrentTypeContext.VoidTypeInstance && currentType is PointerType:

                // assigning 'null' to any pointer type
                // void* anime = null;
                // T* anime2 = null;
                case PointerType ptr6 when currentType is NullType:

                // this is to allow to do this 'int[] arr = null'
                case ArrayType when currentType is NullType:
                case StringType when currentType is NullType:
                    {
                        outExpr = cst;
                        if (castResult != null)
                            castResult.CouldBeCasted = true;
                        break;
                    }
            }

            // try handle class-struct shite
            if (HandleOtherTypes(targetType, currentType, castResult, sourceFile))
            {
                // here if could be normally casted
                outExpr = cst;
            }

            // if there is no way to cast
            if (targetType != currentType && outExpr == null)
            {
                if (castResult == null)
                    MessageHandler.ReportMessage(sourceFile, currentExpr, [HapetType.AsString(currentType), HapetType.AsString(targetType)],
                        ErrorCode.Get(CTEN.TypeCouldNotBeImplCasted));
                outExpr = currentExpr;
            }
            return outExpr;
        }

        private bool HandleOtherTypes(HapetType targetType, HapetType currentType, CastResult castResult = null, ProgramFile sourceFile = null)
        {
            // unroll pointers
            if (targetType is PointerType ptr1 && currentType is PointerType ptr2)
            {
                return HandleOtherTypes(ptr1.TargetType, ptr2.TargetType, castResult);
            }
            // unroll arrays
            else if (targetType is ArrayType arr1 && currentType is ArrayType arr2)
            {
                return HandleOtherTypes(arr1.TargetType, arr2.TargetType, castResult);
            }

            // ok if types are the same or current is inherited from target
            if (targetType == currentType || currentType.IsInheritedFrom(targetType as ClassType))
            {
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return true;
            }

            // handling class-struct casts
            switch (targetType)
            {
                // usually when 'Anime a = null;'
                case ClassType when
                    currentType is NullType:
                // usually when 'Anime a = new Anime();'
                // usually when 'object a = new Anime();'
                case ClassType cls3 when
                    currentType is ClassType cls4 &&
                    (cls4 == cls3 || cls4.IsInheritedFrom(cls3)):
                // usually when 'object a = structInstance;'
                // usually when 'IAnime a = structInstance;'
                case ClassType cls5 when
                    currentType is StructType &&
                    (cls5.Declaration.NameWithNs == "System.Object" ||
                    cls5.Declaration.NameWithNs == "System.ValueType" ||
                    cls5.Declaration.IsInterface):
                // usually when 'TestStruct? a = new TestStruct();'
                case NullableType nt when
                    currentType is StructType strTt &&
                    nt.TargetType is StructType strTt2 &&
                    strTt2 == strTt:
                // usually when 'TestStruct? a = null;'
                case NullableType when
                    currentType is NullType:
                // usually when 'object a = genericInstance;'
                case ClassType cls6 when
                    currentType is GenericType &&
                    (cls6.Declaration.NameWithNs == "System.Object"):
                    {
                        if (castResult != null)
                            castResult.CouldBeCasted = true;
                        return true;
                    }
            }
            
            // handle generic
            if (targetType is GenericType genT)
            {
                // TODO: check constrains
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return true;
            }

            // handling types with generics
            if (HandleTypesWithGenerics(targetType, currentType, castResult, sourceFile))
            {
                // all is ok
                return true;
            }

            return false;
        }

        private bool HandleTypesWithGenerics(HapetType targetType, HapetType currentType, CastResult castResult = null, ProgramFile sourceFile = null)
        {
            // we need to fetch decl's names
            AstIdExpr targetName;
            AstIdExpr currentName;
            if (targetType is DelegateType delT1 && currentType is DelegateType delT2)
            {
                targetName = delT1.TargetDeclaration.Name;
                currentName = delT2.TargetDeclaration.Name;
            }
            else if (targetType is ClassType clsT1 && currentType is ClassType clsT2)
            {
                targetName = clsT1.Declaration.Name;
                currentName = clsT2.Declaration.Name;
            }
            else if (targetType is StructType strT1 && currentType is StructType strT2)
            {
                targetName = strT1.Declaration.Name;
                currentName = strT2.Declaration.Name;
            }
            else
            {
                return false;
            }

            // at first - check that the names are the same
            if (targetName.Name != currentName.Name)
                return false;

            // we need to go all over the generic id types
            if (targetName is AstIdGenericExpr genId1 &&
                currentName is AstIdGenericExpr genId2)
            {
                // shite if different amount of generics
                if (genId1.GenericRealTypes.Count != genId2.GenericRealTypes.Count)
                    return false;

                // go all over and try to cast
                for (int i = 0; i < genId1.GenericRealTypes.Count; i++)
                {
                    var target = genId1.GenericRealTypes[i];
                    var current = genId2.GenericRealTypes[i];
                    var tmpCastResult = new CastResult();
                    TryCastExpr(target.OutType, current, tmpCastResult, sourceFile);
                    // check that could be casted
                    if (!tmpCastResult.CouldBeCasted)
                        return false;
                }

                // all is ok - return true
                if (castResult != null)
                    castResult.CouldBeCasted = true;
                return true;
            }
            return false;
        }
    }
}
