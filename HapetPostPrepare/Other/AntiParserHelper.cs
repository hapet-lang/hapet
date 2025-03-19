using HapetFrontend.Ast;
using HapetFrontend.Ast.Declarations;
using HapetFrontend.Ast.Expressions;
using HapetFrontend.Ast.Statements;
using System.Text;

namespace HapetPostPrepare
{
    public partial class PostPrepare
    {
        public void AntiParseVarDecl(AstVarDecl varDecl, StringBuilder sb, string offset)
        {

        }

        public void AntiParseExpr(AstStatement expr, StringBuilder sb, string offset)
        {
            switch (expr)
            {
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

        }

        public void AntiParseUnaryExpr(AstUnaryExpr unExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseBinaryExpr(AstBinaryExpr binExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParsePointerExpr(AstPointerExpr pointerExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseAddressExpr(AstAddressOfExpr pointerExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseNewExpr(AstNewExpr newExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseArgumentExpr(AstArgumentExpr argExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseIdGenericExpr(AstIdGenericExpr genExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseIdExpr(AstIdExpr idExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseCallExpr(AstCallExpr callExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseCastExpr(AstCastExpr castExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseNestedExpr(AstNestedExpr nestExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseDefaultExpr(AstDefaultExpr defaultExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseArrayExpr(AstArrayExpr arrExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseArrayCreateExpr(AstArrayCreateExpr arrCreateExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseArrayAccessExpr(AstArrayAccessExpr arrAccessExpr, StringBuilder sb, string offset)
        {

        }

        public void AntiParseAssignStmt(AstAssignStmt assignStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseForStmt(AstForStmt forStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseWhileStmt(AstWhileStmt whileStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseIfStmt(AstIfStmt ifStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseSwitchStmt(AstSwitchStmt switchStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseCaseStmt(AstCaseStmt caseStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseBreakStmt(AstBreakContStmt breakStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseReturnStmt(AstReturnStmt returnStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseAttributeStmt(AstAttributeStmt attrStmt, StringBuilder sb, string offset)
        {

        }

        public void AntiParseBaseCtorStmt(AstBaseCtorStmt baseStmt, StringBuilder sb, string offset)
        {

        }
    }
}
