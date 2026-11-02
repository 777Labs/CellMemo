using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using CellMemo.Models;

namespace CellMemo.Services
{
    public class FormulaEvaluationException : Exception
    {
        public string ErrorCode { get; }
        public FormulaEvaluationException(string errorCode) : base(errorCode)
        {
            ErrorCode = errorCode;
        }
    }

    public static class FormulaEvaluator
    {
        public const string ERROR_CYCLE = "#CYCLE!";
        public const string ERROR_DIV0 = "#DIV/0!";
        public const string ERROR_REF = "#REF!";
        public const string ERROR_GENERIC = "#ERROR!";

        public static string Evaluate(
            string formula,
            SheetDocument doc,
            CellAddress currentCell,
            HashSet<CellAddress> visiting)
        {
            if (string.IsNullOrWhiteSpace(formula))
                return string.Empty;

            if (!formula.StartsWith("="))
                return formula;

            string expr = formula.Substring(1).Trim();
            if (string.IsNullOrEmpty(expr))
                return string.Empty;

            if (!visiting.Add(currentCell))
            {
                return ERROR_CYCLE;
            }

            try
            {
                var parser = new FormulaParser(expr, doc, visiting);
                object result = parser.Parse();
                return FormatResult(result);
            }
            catch (FormulaEvaluationException fex)
            {
                return fex.ErrorCode;
            }
            catch
            {
                return ERROR_GENERIC;
            }
            finally
            {
                visiting.Remove(currentCell);
            }
        }

        private static string FormatResult(object? result)
        {
            if (result == null) return string.Empty;
            if (result is double d)
            {
                if (double.IsNaN(d) || double.IsInfinity(d))
                    return ERROR_DIV0;
                // 整数なら小数点以下を表示しない
                if (Math.Abs(d - Math.Round(d)) < 1e-9)
                    return Math.Round(d).ToString(CultureInfo.InvariantCulture);
                return d.ToString("G15", CultureInfo.InvariantCulture);
            }
            if (result is bool b)
            {
                return b ? "TRUE" : "FALSE";
            }
            return result.ToString() ?? string.Empty;
        }

        private enum TokenType
        {
            Number,
            String,
            Identifier,
            CellRef,
            CellRange,
            Operator,
            Comma,
            OpenParen,
            CloseParen,
            End
        }

        private struct Token
        {
            public TokenType Type;
            public string Value;
            public double NumberValue;
            public CellAddress Address1;
            public CellAddress Address2;
        }

        private class FormulaParser
        {
            private readonly string _text;
            private readonly SheetDocument _doc;
            private readonly HashSet<CellAddress> _visiting;
            private int _pos;
            private Token _currentToken;

            public FormulaParser(string text, SheetDocument doc, HashSet<CellAddress> visiting)
            {
                _text = text;
                _doc = doc;
                _visiting = visiting;
                _pos = 0;
                Advance();
            }

            public object Parse()
            {
                object val = ParseComparison();
                if (_currentToken.Type != TokenType.End)
                {
                    throw new FormulaEvaluationException(ERROR_GENERIC);
                }
                return val;
            }

            private void Advance()
            {
                SkipWhitespace();
                if (_pos >= _text.Length)
                {
                    _currentToken = new Token { Type = TokenType.End };
                    return;
                }

                char c = _text[_pos];

                if (c == '(')
                {
                    _pos++;
                    _currentToken = new Token { Type = TokenType.OpenParen, Value = "(" };
                    return;
                }
                if (c == ')')
                {
                    _pos++;
                    _currentToken = new Token { Type = TokenType.CloseParen, Value = ")" };
                    return;
                }
                if (c == ',')
                {
                    _pos++;
                    _currentToken = new Token { Type = TokenType.Comma, Value = "," };
                    return;
                }

                // 文字列リテラル
                if (c == '"')
                {
                    _pos++;
                    var sb = new StringBuilder();
                    while (_pos < _text.Length)
                    {
                        if (_text[_pos] == '"')
                        {
                            if (_pos + 1 < _text.Length && _text[_pos + 1] == '"')
                            {
                                sb.Append('"');
                                _pos += 2;
                            }
                            else
                            {
                                _pos++;
                                break;
                            }
                        }
                        else
                        {
                            sb.Append(_text[_pos]);
                            _pos++;
                        }
                    }
                    _currentToken = new Token { Type = TokenType.String, Value = sb.ToString() };
                    return;
                }

                // 2文字演算子 (<>, <=, >=)
                if (_pos + 1 < _text.Length)
                {
                    string two = _text.Substring(_pos, 2);
                    if (two == "<>" || two == "<=" || two == ">=")
                    {
                        _pos += 2;
                        _currentToken = new Token { Type = TokenType.Operator, Value = two };
                        return;
                    }
                }

                // 1文字演算子
                if ("+-*/^=<><>".Contains(c))
                {
                    _pos++;
                    _currentToken = new Token { Type = TokenType.Operator, Value = c.ToString() };
                    return;
                }

                // 数値
                if (char.IsDigit(c) || (c == '.' && _pos + 1 < _text.Length && char.IsDigit(_text[_pos + 1])))
                {
                    int start = _pos;
                    bool hasDot = false;
                    while (_pos < _text.Length && (char.IsDigit(_text[_pos]) || (!hasDot && _text[_pos] == '.')))
                    {
                        if (_text[_pos] == '.') hasDot = true;
                        _pos++;
                    }
                    string numStr = _text.Substring(start, _pos - start);
                    if (double.TryParse(numStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                    {
                        _currentToken = new Token { Type = TokenType.Number, NumberValue = num, Value = numStr };
                        return;
                    }
                    throw new FormulaEvaluationException(ERROR_GENERIC);
                }

                // 識別子またはセル参照・セル範囲
                if (char.IsLetter(c) || c == '_' || c == '$')
                {
                    int start = _pos;
                    while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_' || _text[_pos] == '$'))
                    {
                        _pos++;
                    }
                    string ident = _text.Substring(start, _pos - start);

                    // セル範囲 (例: A1:B5) の確認
                    if (_pos < _text.Length && _text[_pos] == ':')
                    {
                        _pos++;
                        int start2 = _pos;
                        while (_pos < _text.Length && (char.IsLetterOrDigit(_text[_pos]) || _text[_pos] == '_' || _text[_pos] == '$'))
                        {
                            _pos++;
                        }
                        string ident2 = _text.Substring(start2, _pos - start2);
                        if (CellAddress.TryParse(ident, out var addr1) && CellAddress.TryParse(ident2, out var addr2))
                        {
                            _currentToken = new Token
                            {
                                Type = TokenType.CellRange,
                                Address1 = addr1,
                                Address2 = addr2,
                                Value = $"{ident}:{ident2}"
                            };
                            return;
                        }
                        throw new FormulaEvaluationException(ERROR_REF);
                    }

                    if (CellAddress.TryParse(ident, out var addr))
                    {
                        _currentToken = new Token
                        {
                            Type = TokenType.CellRef,
                            Address1 = addr,
                            Value = ident
                        };
                        return;
                    }

                    _currentToken = new Token { Type = TokenType.Identifier, Value = ident.ToUpperInvariant() };
                    return;
                }

                throw new FormulaEvaluationException(ERROR_GENERIC);
            }

            private void SkipWhitespace()
            {
                while (_pos < _text.Length && char.IsWhiteSpace(_text[_pos]))
                {
                    _pos++;
                }
            }

            private object ParseComparison()
            {
                object left = ParseTerm();

                while (_currentToken.Type == TokenType.Operator &&
                       (_currentToken.Value == "=" || _currentToken.Value == "<>" ||
                        _currentToken.Value == "<" || _currentToken.Value == "<=" ||
                        _currentToken.Value == ">" || _currentToken.Value == ">="))
                {
                    string op = _currentToken.Value;
                    Advance();
                    object right = ParseTerm();

                    left = EvaluateComparison(left, op, right);
                }

                return left;
            }

            private bool EvaluateComparison(object left, string op, object right)
            {
                double d1 = ToDouble(left);
                double d2 = ToDouble(right);
                int cmp = d1.CompareTo(d2);

                return op switch
                {
                    "=" => cmp == 0,
                    "<>" => cmp != 0,
                    "<" => cmp < 0,
                    "<=" => cmp <= 0,
                    ">" => cmp > 0,
                    ">=" => cmp >= 0,
                    _ => throw new FormulaEvaluationException(ERROR_GENERIC)
                };
            }

            private object ParseTerm()
            {
                object left = ParseFactor();

                while (_currentToken.Type == TokenType.Operator &&
                       (_currentToken.Value == "+" || _currentToken.Value == "-"))
                {
                    string op = _currentToken.Value;
                    Advance();
                    object right = ParseFactor();

                    double d1 = ToDouble(left);
                    double d2 = ToDouble(right);

                    left = op == "+" ? d1 + d2 : d1 - d2;
                }

                return left;
            }

            private object ParseFactor()
            {
                object left = ParsePower();

                while (_currentToken.Type == TokenType.Operator &&
                       (_currentToken.Value == "*" || _currentToken.Value == "/"))
                {
                    string op = _currentToken.Value;
                    Advance();
                    object right = ParsePower();

                    double d1 = ToDouble(left);
                    double d2 = ToDouble(right);

                    if (op == "*")
                    {
                        left = d1 * d2;
                    }
                    else
                    {
                        if (Math.Abs(d2) < 1e-12)
                            throw new FormulaEvaluationException(ERROR_DIV0);
                        left = d1 / d2;
                    }
                }

                return left;
            }

            private object ParsePower()
            {
                object left = ParseUnary();

                while (_currentToken.Type == TokenType.Operator && _currentToken.Value == "^")
                {
                    Advance();
                    object right = ParseUnary();
                    double d1 = ToDouble(left);
                    double d2 = ToDouble(right);
                    left = Math.Pow(d1, d2);
                }

                return left;
            }

            private object ParseUnary()
            {
                if (_currentToken.Type == TokenType.Operator &&
                    (_currentToken.Value == "+" || _currentToken.Value == "-"))
                {
                    string op = _currentToken.Value;
                    Advance();
                    object val = ParsePrimary();
                    double d = ToDouble(val);
                    return op == "-" ? -d : d;
                }

                return ParsePrimary();
            }

            private object ParsePrimary()
            {
                if (_currentToken.Type == TokenType.Number)
                {
                    double val = _currentToken.NumberValue;
                    Advance();
                    return val;
                }

                if (_currentToken.Type == TokenType.String)
                {
                    string s = _currentToken.Value;
                    Advance();
                    return s;
                }

                if (_currentToken.Type == TokenType.OpenParen)
                {
                    Advance();
                    object val = ParseComparison();
                    if (_currentToken.Type != TokenType.CloseParen)
                        throw new FormulaEvaluationException(ERROR_GENERIC);
                    Advance();
                    return val;
                }

                if (_currentToken.Type == TokenType.CellRef)
                {
                    var addr = _currentToken.Address1;
                    Advance();
                    return GetCellValue(addr);
                }

                if (_currentToken.Type == TokenType.CellRange)
                {
                    // 単体セル範囲が単項として使われた場合（例: SUM(A1:A5) 以外の文脈）
                    var a1 = _currentToken.Address1;
                    var a2 = _currentToken.Address2;
                    Advance();
                    return GetRangeValues(a1, a2);
                }

                if (_currentToken.Type == TokenType.Identifier)
                {
                    string funcName = _currentToken.Value;
                    Advance();
                    if (_currentToken.Type != TokenType.OpenParen)
                        throw new FormulaEvaluationException(ERROR_GENERIC);
                    Advance();

                    var args = new List<object>();
                    if (_currentToken.Type != TokenType.CloseParen)
                    {
                        while (true)
                        {
                            if (_currentToken.Type == TokenType.CellRange)
                            {
                                var a1 = _currentToken.Address1;
                                var a2 = _currentToken.Address2;
                                Advance();
                                args.Add(GetRangeValues(a1, a2));
                            }
                            else
                            {
                                object arg = ParseComparison();
                                args.Add(arg);
                            }

                            if (_currentToken.Type == TokenType.Comma)
                            {
                                Advance();
                                continue;
                            }
                            if (_currentToken.Type == TokenType.CloseParen)
                            {
                                break;
                            }
                            throw new FormulaEvaluationException(ERROR_GENERIC);
                        }
                    }

                    Advance(); // consume ')'
                    return EvaluateFunction(funcName, args);
                }

                throw new FormulaEvaluationException(ERROR_GENERIC);
            }

            private object GetCellValue(CellAddress address)
            {
                var cell = _doc.GetCell(address);
                if (cell == null || string.IsNullOrWhiteSpace(cell.RawText))
                    return 0.0;

                if (cell.IsFormula)
                {
                    string eval = FormulaEvaluator.Evaluate(cell.RawText, _doc, address, _visiting);
                    if (eval.StartsWith("#"))
                        throw new FormulaEvaluationException(eval);
                    if (double.TryParse(eval, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                        return d;
                    return eval;
                }

                if (double.TryParse(cell.RawText, NumberStyles.Float, CultureInfo.InvariantCulture, out double num))
                    return num;

                return cell.RawText;
            }

            private List<object> GetRangeValues(CellAddress a1, CellAddress a2)
            {
                int rStart = Math.Min(a1.Row, a2.Row);
                int rEnd = Math.Max(a1.Row, a2.Row);
                int cStart = Math.Min(a1.Col, a2.Col);
                int cEnd = Math.Max(a1.Col, a2.Col);

                var list = new List<object>();
                for (int r = rStart; r <= rEnd; r++)
                {
                    for (int c = cStart; c <= cEnd; c++)
                    {
                        var addr = new CellAddress(r, c);
                        var cell = _doc.GetCell(addr);
                        if (cell == null || string.IsNullOrWhiteSpace(cell.RawText))
                            continue;

                        if (cell.IsFormula)
                        {
                            string eval = FormulaEvaluator.Evaluate(cell.RawText, _doc, addr, _visiting);
                            if (eval.StartsWith("#"))
                                throw new FormulaEvaluationException(eval);
                            if (double.TryParse(eval, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                                list.Add(d);
                            else
                                list.Add(eval);
                        }
                        else
                        {
                            if (double.TryParse(cell.RawText, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                                list.Add(d);
                            else
                                list.Add(cell.RawText);
                        }
                    }
                }
                return list;
            }

            private object EvaluateFunction(string name, List<object> args)
            {
                name = name.ToUpperInvariant();

                switch (name)
                {
                    case "SUM":
                        return FuncSum(args);
                    case "AVERAGE":
                        return FuncAverage(args);
                    case "COUNT":
                        return FuncCount(args);
                    case "COUNTA":
                        return FuncCountA(args);
                    case "MIN":
                        return FuncMin(args);
                    case "MAX":
                        return FuncMax(args);
                    case "ROUND":
                        return FuncRound(args);
                    case "ABS":
                        return FuncAbs(args);
                    case "IF":
                        return FuncIf(args);
                    case "CONCAT":
                        return FuncConcat(args);
                    default:
                        throw new FormulaEvaluationException(ERROR_GENERIC);
                }
            }

            private void FlattenNumbers(List<object> args, List<double> numbers)
            {
                foreach (var arg in args)
                {
                    if (arg is List<object> subList)
                    {
                        FlattenNumbers(subList, numbers);
                    }
                    else if (arg is double d)
                    {
                        numbers.Add(d);
                    }
                    else if (arg is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    {
                        numbers.Add(parsed);
                    }
                }
            }

            private double FuncSum(List<object> args)
            {
                var nums = new List<double>();
                FlattenNumbers(args, nums);
                double sum = 0;
                foreach (var n in nums) sum += n;
                return sum;
            }

            private double FuncAverage(List<object> args)
            {
                var nums = new List<double>();
                FlattenNumbers(args, nums);
                if (nums.Count == 0)
                    throw new FormulaEvaluationException(ERROR_DIV0);
                double sum = 0;
                foreach (var n in nums) sum += n;
                return sum / nums.Count;
            }

            private double FuncCount(List<object> args)
            {
                var nums = new List<double>();
                FlattenNumbers(args, nums);
                return nums.Count;
            }

            private double FuncCountA(List<object> args)
            {
                int count = 0;
                void CountItems(List<object> list)
                {
                    foreach (var item in list)
                    {
                        if (item is List<object> sub)
                        {
                            CountItems(sub);
                        }
                        else if (item != null && !string.IsNullOrEmpty(item.ToString()))
                        {
                            count++;
                        }
                    }
                }
                CountItems(args);
                return count;
            }

            private double FuncMin(List<object> args)
            {
                var nums = new List<double>();
                FlattenNumbers(args, nums);
                if (nums.Count == 0) return 0.0;
                double min = double.MaxValue;
                foreach (var n in nums) if (n < min) min = n;
                return min;
            }

            private double FuncMax(List<object> args)
            {
                var nums = new List<double>();
                FlattenNumbers(args, nums);
                if (nums.Count == 0) return 0.0;
                double max = double.MinValue;
                foreach (var n in nums) if (n > max) max = n;
                return max;
            }

            private double FuncRound(List<object> args)
            {
                if (args.Count < 1) throw new FormulaEvaluationException(ERROR_GENERIC);
                double val = ToDouble(args[0]);
                int digits = 0;
                if (args.Count >= 2) digits = (int)Math.Round(ToDouble(args[1]));
                return Math.Round(val, Math.Clamp(digits, 0, 15), MidpointRounding.AwayFromZero);
            }

            private double FuncAbs(List<object> args)
            {
                if (args.Count < 1) throw new FormulaEvaluationException(ERROR_GENERIC);
                return Math.Abs(ToDouble(args[0]));
            }

            private object FuncIf(List<object> args)
            {
                if (args.Count < 2) throw new FormulaEvaluationException(ERROR_GENERIC);
                bool condition = ToBool(args[0]);
                if (condition)
                    return args[1];
                return args.Count >= 3 ? args[2] : 0.0;
            }

            private string FuncConcat(List<object> args)
            {
                var sb = new StringBuilder();
                void AppendItems(List<object> list)
                {
                    foreach (var item in list)
                    {
                        if (item is List<object> sub)
                            AppendItems(sub);
                        else if (item != null)
                            sb.Append(item.ToString());
                    }
                }
                AppendItems(args);
                return sb.ToString();
            }

            private double ToDouble(object val)
            {
                if (val is double d) return d;
                if (val is bool b) return b ? 1.0 : 0.0;
                if (val is string s && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed))
                    return parsed;
                return 0.0;
            }

            private bool ToBool(object val)
            {
                if (val is bool b) return b;
                if (val is double d) return Math.Abs(d) > 1e-12;
                if (val is string s)
                {
                    if (bool.TryParse(s, out bool pb)) return pb;
                    if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double pd)) return Math.Abs(pd) > 1e-12;
                }
                return false;
            }
        }
    }
}
