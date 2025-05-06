using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using HapetFrontend.Helpers;
using HapetFrontend.Parsing;
using System.Diagnostics;
using System.Text;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void AntiParseLiterals(AstStatement expr, StringBuilder sb, string offset)
        {
            switch (expr)
            {
                // consts and literals
                case AstNumberExpr n:
                    sb.Append(n.Data);
                    break;
                case AstStringExpr s:
                    sb.Append($"\"{s.StringValue}\"");
                    break;
                case AstCharExpr c:
                    sb.Append($"\'{c.CharValue}\'");
                    break;
                case AstBoolExpr b:
                    sb.Append(b.BoolValue ? "true" : "false");
                    break;
                case AstNullExpr:
                    sb.Append("null");
                    break;
            }
        }

        public void AntiParseVarDecl(AstVarDecl varDecl, StringBuilder sb, string offset)
        {
            AntiParseExpr(varDecl.Type, sb, offset);
            sb.Append(' ');
            AntiParseExpr(varDecl.Name, sb, offset);

            if (varDecl.Initializer != null)
            {
                sb.Append(" = ");
                AntiParseExpr(varDecl.Initializer, sb, offset);
            }
        }

        [SkipInStackFrame]
        [DebuggerStepThrough]
        public void AntiParseExpr(AstStatement expr, StringBuilder sb, string offset)
        {
            switch (expr)
            {
                // consts and literals
                case AstNumberExpr:
                case AstStringExpr:
                case AstCharExpr:
                case AstBoolExpr:
                case AstNullExpr:
                    AntiParseLiterals(expr, sb, offset);
                    break;

                // special case at least for 'for' loop
                // when 'for (int i = 0;...)' where 'int i' 
                // would not be handled by blockExpr
                case AstVarDecl varDecl:
                    AntiParseVarDecl(varDecl, sb, offset);
                    break;

                case AstBlockExpr blockExpr:
                    AntiParseBlockExpr(blockExpr, sb, offset);
                    break;
                case AstUnaryExpr unExpr:
                    AntiParseUnaryExpr(unExpr, sb, offset);
                    break;
                case AstBinaryExpr binExpr:
                    AntiParseBinaryExpr(binExpr, sb, offset);
                    break;
                case AstPointerExpr pointerExpr:
                    AntiParsePointerExpr(pointerExpr, sb, offset);
                    break;
                case AstAddressOfExpr addrExpr:
                    AntiParseAddressExpr(addrExpr, sb, offset);
                    break;
                case AstNewExpr newExpr:
                    AntiParseNewExpr(newExpr, sb, offset);
                    break;
                case AstArgumentExpr argumentExpr:
                    AntiParseArgumentExpr(argumentExpr, sb, offset);
                    break;
                case AstIdGenericExpr genExpr:
                    AntiParseIdGenericExpr(genExpr, sb, offset);
                    break;
                case AstIdExpr idExpr:
                    AntiParseIdExpr(idExpr, sb, offset);
                    break;
                case AstCallExpr callExpr:
                    AntiParseCallExpr(callExpr, sb, offset);
                    break;
                case AstCastExpr castExpr:
                    AntiParseCastExpr(castExpr, sb, offset);
                    break;
                case AstNestedExpr nestExpr:
                    AntiParseNestedExpr(nestExpr, sb, offset);
                    break;
                case AstDefaultExpr defExpr:
                    AntiParseDefaultExpr(defExpr, sb, offset);
                    break;
                case AstArrayExpr arrayExpr:
                    AntiParseArrayExpr(arrayExpr, sb, offset);
                    break;
                case AstArrayCreateExpr arrayCreateExpr:
                    AntiParseArrayCreateExpr(arrayCreateExpr, sb, offset);
                    break;
                case AstArrayAccessExpr arrayAccExpr:
                    AntiParseArrayAccessExpr(arrayAccExpr, sb, offset);
                    break;
                case AstTernaryExpr terExpr:
                    AntiParseTernaryExpr(terExpr, sb, offset);
                    break;

                // statements
                case AstAssignStmt assignStmt:
                    AntiParseAssignStmt(assignStmt, sb, offset);
                    break;
                case AstForStmt forStmt:
                    AntiParseForStmt(forStmt, sb, offset);
                    break;
                case AstWhileStmt whileStmt:
                    AntiParseWhileStmt(whileStmt, sb, offset);
                    break;
                case AstIfStmt ifStmt:
                    AntiParseIfStmt(ifStmt, sb, offset);
                    break;
                case AstSwitchStmt switchStmt:
                    AntiParseSwitchStmt(switchStmt, sb, offset);
                    break;
                case AstCaseStmt caseStmt:
                    AntiParseCaseStmt(caseStmt, sb, offset);
                    break;
                case AstBreakContStmt breakStmt:
                    AntiParseBreakStmt(breakStmt, sb, offset);
                    break;
                case AstReturnStmt returnStmt:
                    AntiParseReturnStmt(returnStmt, sb, offset);
                    break;
                case AstAttributeStmt attrStmt:
                    AntiParseAttributeStmt(attrStmt, sb, offset);
                    break;
                case AstBaseCtorStmt baseStmt:
                    AntiParseBaseCtorStmt(baseStmt, sb, offset);
                    break;

                case AstUsingStmt usingStmt:
                    AntiParseUsingStmt(usingStmt, sb, offset);
                    break;
                // TODO: check other expressions

                default:
                    {
                        // TODO: anything to do here?
                        break;
                    }
            }
        }

        public void AntiParseBlockExpr(AstBlockExpr blockExpr, StringBuilder sb, string offset)
        {
            sb.Append($"{offset}{{\n");
            foreach (var stmt in blockExpr.Statements)
            {
                sb.Append($"{offset + _fourSpaces}");
                AntiParseExpr(stmt, sb, offset + _fourSpaces);

                // do not ; after this stmts
                if (stmt is not AstBlockExpr &&
                    stmt is not AstIfStmt && 
                    stmt is not AstSwitchStmt &&
                    stmt is not AstForStmt && 
                    stmt is not AstWhileStmt &&
                    stmt is not AstAttributeStmt)
                    sb.Append(";\n");
            }
            sb.Append($"{offset}}}\n");
        }

        public void AntiParseUnaryExpr(AstUnaryExpr unExpr, StringBuilder sb, string offset)
        {
            sb.Append($"({unExpr.Operator}");
            AntiParseExpr(unExpr.SubExpr, sb, offset);
            sb.Append(')');
        }

        public void AntiParseBinaryExpr(AstBinaryExpr binExpr, StringBuilder sb, string offset)
        {
            sb.Append('(');
            AntiParseExpr(binExpr.Left, sb, offset);
            sb.Append($" {binExpr.Operator} ");
            AntiParseExpr(binExpr.Right, sb, offset);
            sb.Append(')');
        }

        public void AntiParsePointerExpr(AstPointerExpr pointerExpr, StringBuilder sb, string offset)
        {
            if (pointerExpr.IsDereference)
            {
                sb.Append('(');
                sb.Append('*');
                AntiParseExpr(pointerExpr.SubExpression, sb, offset);
                sb.Append(')');
            }
            else
            {
                AntiParseExpr(pointerExpr.SubExpression, sb, offset);
                sb.Append('*');
            }
        }

        public void AntiParseAddressExpr(AstAddressOfExpr addrExpr, StringBuilder sb, string offset)
        {
            sb.Append('&');
            AntiParseExpr(addrExpr.SubExpression, sb, offset);
        }

        public void AntiParseNewExpr(AstNewExpr newExpr, StringBuilder sb, string offset)
        {
            sb.Append('(');

            sb.Append("new ");
            AntiParseExpr(newExpr.TypeName, sb, offset);
            sb.Append('(');
            for (int i = 0; i < newExpr.Arguments.Count; ++i)
            {
                AntiParseExpr(newExpr.Arguments[i], sb, offset);
                if (i != newExpr.Arguments.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(')');

            sb.Append(')');
        }

        public void AntiParseArgumentExpr(AstArgumentExpr argExpr, StringBuilder sb, string offset)
        {
            // TODO: param name
            AntiParseExpr(argExpr.Expr, sb, offset);
        }

        public void AntiParseIdGenericExpr(AstIdGenericExpr genExpr, StringBuilder sb, string offset)
        {
            sb.Append(GenericsHelper.GetNameFromAst(genExpr, _compiler.MessageHandler));
        }

        public void AntiParseIdExpr(AstIdExpr idExpr, StringBuilder sb, string offset)
        {
            sb.Append(GenericsHelper.GetNameFromAst(idExpr, _compiler.MessageHandler));
        }

        public void AntiParseCallExpr(AstCallExpr callExpr, StringBuilder sb, string offset)
        {
            if (callExpr.TypeOrObjectName != null)
            {
                AntiParseExpr(callExpr.TypeOrObjectName, sb, offset);
                sb.Append('.');
            }
            sb.Append(GenericsHelper.GetNameFromAst(callExpr.FuncName, _compiler.MessageHandler));
            sb.Append('(');
            for (int i = 0; i < callExpr.Arguments.Count; ++i)
            {
                AntiParseExpr(callExpr.Arguments[i], sb, offset);
                if (i != callExpr.Arguments.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(')');
        }

        public void AntiParseCastExpr(AstCastExpr castExpr, StringBuilder sb, string offset)
        {
            sb.Append('(');

            sb.Append('(');
            AntiParseExpr(castExpr.TypeExpr, sb, offset);
            sb.Append(')');

            sb.Append('(');
            AntiParseExpr(castExpr.SubExpression, sb, offset);
            sb.Append(')');

            sb.Append(')');
        }

        public void AntiParseNestedExpr(AstNestedExpr nestExpr, StringBuilder sb, string offset)
        {
            if (nestExpr.LeftPart != null)
            {
                AntiParseExpr(nestExpr.LeftPart, sb, offset);
                sb.Append('.');
            }
            AntiParseExpr(nestExpr.RightPart, sb, offset);
        }

        public void AntiParseDefaultExpr(AstDefaultExpr defaultExpr, StringBuilder sb, string offset)
        {
            sb.Append("default");
        }

        public void AntiParseArrayExpr(AstArrayExpr arrExpr, StringBuilder sb, string offset)
        {
            AntiParseExpr(arrExpr.SubExpression, sb, offset);
            sb.Append("[]");
        }

        public void AntiParseArrayCreateExpr(AstArrayCreateExpr arrCreateExpr, StringBuilder sb, string offset)
        {
            sb.Append('(');

            sb.Append("new ");
            AntiParseExpr(arrCreateExpr.TypeName, sb, offset);
            foreach (var sz in arrCreateExpr.SizeExprs)
            {
                sb.Append('[');
                AntiParseExpr(sz, sb, offset);
                sb.Append(']');
            }

            sb.Append('{');
            for (int i = 0; i < arrCreateExpr.Elements.Count; ++i)
            {
                AntiParseExpr(arrCreateExpr.Elements[i], sb, offset);
                if (i != arrCreateExpr.Elements.Count - 1)
                    sb.Append(", ");
            }
            sb.Append('}');

            sb.Append(')');
        }

        public void AntiParseArrayAccessExpr(AstArrayAccessExpr arrAccessExpr, StringBuilder sb, string offset)
        {
            AntiParseExpr(arrAccessExpr.ObjectName, sb, offset);
            sb.Append('[');
            AntiParseExpr(arrAccessExpr.ParameterExpr, sb, offset);
            sb.Append(']');
        }

        public void AntiParseTernaryExpr(AstTernaryExpr terExpr, StringBuilder sb, string offset)
        {
            sb.Append('(');

            AntiParseExpr(terExpr.Condition, sb, offset);
            sb.Append(" ? ");
            AntiParseExpr(terExpr.TrueExpr, sb, offset);
            sb.Append(" : ");
            AntiParseExpr(terExpr.FalseExpr, sb, offset);

            sb.Append(')');
        }

        public void AntiParseAssignStmt(AstAssignStmt assignStmt, StringBuilder sb, string offset)
        {
            AntiParseExpr(assignStmt.Target, sb, offset);
            sb.Append(" = ");
            AntiParseExpr(assignStmt.Value, sb, offset);
        }

        public void AntiParseForStmt(AstForStmt forStmt, StringBuilder sb, string offset)
        {
            sb.Append("for (");
            AntiParseExpr(forStmt.FirstArgument, sb, offset);
            sb.Append("; ");
            AntiParseExpr(forStmt.SecondArgument, sb, offset);
            sb.Append("; ");
            AntiParseExpr(forStmt.ThirdArgument, sb, offset);
            sb.Append(")\n");

            AntiParseExpr(forStmt.Body, sb, offset);
        }

        public void AntiParseWhileStmt(AstWhileStmt whileStmt, StringBuilder sb, string offset)
        {
            sb.Append("while (");
            AntiParseExpr(whileStmt.Condition, sb, offset);
            sb.Append(")\n");

            AntiParseExpr(whileStmt.Body, sb, offset);
        }

        public void AntiParseIfStmt(AstIfStmt ifStmt, StringBuilder sb, string offset)
        {
            sb.Append("if (");
            AntiParseExpr(ifStmt.Condition, sb, offset);
            sb.Append(")\n");

            AntiParseExpr(ifStmt.BodyTrue, sb, offset);

            if (ifStmt.BodyFalse != null)
            {
                sb.Append($"{offset}else \n");
                AntiParseExpr(ifStmt.BodyFalse, sb, offset);
            }
        }

        public void AntiParseSwitchStmt(AstSwitchStmt switchStmt, StringBuilder sb, string offset)
        {
            sb.Append("switch (");
            AntiParseExpr(switchStmt.SubExpression, sb, offset);
            sb.Append(")\n");

            sb.Append($"{offset}{{\n");
            foreach (var cs in switchStmt.Cases)
            {
                sb.Append($"{offset + _fourSpaces}");
                AntiParseExpr(cs, sb, offset + _fourSpaces);
                //sb.Append(";\n");
            }
            sb.Append($"{offset}}}\n");
        }

        public void AntiParseCaseStmt(AstCaseStmt caseStmt, StringBuilder sb, string offset)
        {
            if (caseStmt.IsDefaultCase)
            {
                sb.Append("default \n");
            }
            else
            {
                sb.Append("case (");
                AntiParseExpr(caseStmt.Pattern, sb, offset);
                sb.Append(")\n");
            }
            
            if (!caseStmt.IsFallingCase)
            {
                AntiParseExpr(caseStmt.Body, sb, offset);
            }
        }

        public void AntiParseBreakStmt(AstBreakContStmt breakStmt, StringBuilder sb, string offset)
        {
            if (breakStmt.IsBreak)
                sb.Append("break");
            else
                sb.Append("continue");
        }

        public void AntiParseReturnStmt(AstReturnStmt returnStmt, StringBuilder sb, string offset)
        {
            sb.Append("return");
            if (returnStmt.ReturnExpression != null)
            {
                sb.Append(' ');
                AntiParseExpr(returnStmt.ReturnExpression, sb, offset);
            }
        }

        public void AntiParseAttributeStmt(AstAttributeStmt attrStmt, StringBuilder sb, string offset)
        {
            sb.Append('[');
            AntiParseExpr(attrStmt.AttributeName, sb, offset);
            if (attrStmt.Arguments.Count > 0)
            {
                sb.Append('(');
                for (int i = 0; i < attrStmt.Arguments.Count; i++)
                {
                    var arg = attrStmt.Arguments[i];
                    AntiParseExpr(arg, sb, offset);

                    if (i < attrStmt.Arguments.Count - 1)
                        sb.Append(", ");
                }
                sb.Append(')');
            }
            sb.Append("]\n");
        }

        public void AntiParseBaseCtorStmt(AstBaseCtorStmt baseStmt, StringBuilder sb, string offset)
        {
            sb.Append("base(");
            for (int i = 0; i < baseStmt.Arguments.Count; ++i)
            {
                AntiParseExpr(baseStmt.Arguments[i], sb, offset);
                if (i != baseStmt.Arguments.Count - 1)
                    sb.Append(", ");
            }
            sb.Append(')');
        }

        public void AntiParseUsingStmt(AstUsingStmt usingStmt, StringBuilder sb, string offset)
        {
            sb.Append("using ");
            AntiParseExpr(usingStmt.Namespace, sb, offset);
            sb.Append(";\n");
        }
    }
}
