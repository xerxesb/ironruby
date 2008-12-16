/* ****************************************************************************
 *
 * Copyright (c) Microsoft Corporation. 
 *
 * This source code is subject to terms and conditions of the Microsoft Public License. A 
 * copy of the license can be found in the License.html file at the root of this distribution. If 
 * you cannot locate the  Microsoft Public License, please send an email to 
 * dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
 * by the terms of the Microsoft Public License.
 *
 * You must not remove this notice, or any other, from this software.
 *
 *
 * ***************************************************************************/

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;

using System.Dynamic.Utils;
using System.Threading;

namespace System.Linq.Expressions {
    /// <summary>
    /// Defines a block where variables are defined. The compiler will
    /// automatically close over these variables if they're referenced in a
    /// nested LambdaExpession.
    /// 
    /// Specialized subclasses exist which actually implement the storage
    /// for the BlockExpression.
    /// </summary>
    public class BlockExpression : Expression {

        public ReadOnlyCollection<Expression> Expressions {
            get { return GetOrMakeExpressions(); }
        }

        /// <summary>
        /// The variables in this block.
        /// </summary>
        public ReadOnlyCollection<ParameterExpression> Variables {
            get {
                return GetOrMakeVariables();
            }
        }

        internal BlockExpression() {
        }

        internal override Expression Accept(ExpressionVisitor visitor) {
            return visitor.VisitBlock(this);
        }

        protected override ExpressionType GetNodeKind() {
            return ExpressionType.Block;
        }

        protected override Type GetExpressionType() {
            return GetExpression(ExpressionCount - 1).Type;
        }

        internal virtual Expression GetExpression(int index) {
            throw ContractUtils.Unreachable;
        }

        internal virtual int ExpressionCount {
            get {
                throw ContractUtils.Unreachable;
            }
        }

        internal virtual ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            throw ContractUtils.Unreachable;
        }

        internal virtual ParameterExpression GetVariable(int index) {
            throw ContractUtils.Unreachable;
        }

        internal virtual int VariableCount {
            get {
                return 0;
            }
        }

        internal virtual ReadOnlyCollection<ParameterExpression> GetOrMakeVariables() {
            return EmptyReadOnlyCollection<ParameterExpression>.Instance;
        }

        /// <summary>
        /// Makes a copy of this node replacing the parameters/args with the provided values.  The 
        /// shape of the parameters/args needs to match the shape of the current block - in other
        /// words there should be the same # of parameters and args.
        /// 
        /// parameters can be null in which case the existing parameters are used.
        /// 
        /// This helper is provided to allow re-writing of nodes to not depend on the specific optimized
        /// subclass of BlockExpression which is being used. 
        /// </summary>
        internal virtual BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            throw ContractUtils.Unreachable;
        }

        /// <summary>
        /// Helper used for ensuring we only return 1 instance of a ReadOnlyCollection of T.
        /// 
        /// This is similar to the ReturnReadOnly which only takes a single argument. This version
        /// supports nodes which hold onto 5 Expressions and puts all of the arguments into the
        /// ReadOnlyCollection.
        /// 
        /// Ultimately this means if we create the ROC we will be slightly more wasteful as we'll
        /// have a ROC + some fields in the type.  The DLR internally avoids accessing anything
        /// which would force the ROC to be created.
        /// 
        /// This is used by BlockExpression5 and MethodCallExpression5.
        /// </summary>
        internal static ReadOnlyCollection<Expression> ReturnReadOnlyExpressions(BlockExpression provider, ref object collection) {
            Expression tObj = collection as Expression;
            if (tObj != null) {
                // otherwise make sure only one ROC ever gets exposed
                Interlocked.CompareExchange(
                    ref collection,
                    new ReadOnlyCollection<Expression>(new BlockExpressionList(provider, tObj)),
                    tObj
                );
            }

            // and return what is not guaranteed to be a ROC
            return (ReadOnlyCollection<Expression>)collection;
        }       
    }

    #region Specialized Subclasses

    internal sealed class Block2 : BlockExpression {
        private object _arg0;                   // storage for the 1st argument or a ROC.  See IArgumentProvider
        private readonly Expression _arg1;      // storage for the 2nd argument.

        internal Block2(Expression arg0, Expression arg1) {
            _arg0 = arg0;
            _arg1 = arg1;
        }

        internal override Expression GetExpression(int index) {
            switch (index) {
                case 0: return ReturnObject<Expression>(_arg0);
                case 1: return _arg1;
                default: throw new InvalidOperationException();
            }
        }

        internal override int ExpressionCount {
            get {
                return 2;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnlyExpressions(this, ref _arg0);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == 2);
            Debug.Assert(variables == null || variables.Count == 0);

            return new Block2(args[0], args[1]);
        }
    }

    internal sealed class Block3 : BlockExpression {
        private object _arg0;                       // storage for the 1st argument or a ROC.  See IArgumentProvider
        private readonly Expression _arg1, _arg2;   // storage for the 2nd and 3rd arguments.

        internal Block3(Expression arg0, Expression arg1, Expression arg2) {
            _arg0 = arg0;
            _arg1 = arg1;
            _arg2 = arg2;
        }

        internal override Expression GetExpression(int index) {
            switch (index) {
                case 0: return ReturnObject<Expression>(_arg0);
                case 1: return _arg1;
                case 2: return _arg2;
                default: throw new InvalidOperationException();
            }
        }

        internal override int ExpressionCount {
            get {
                return 3;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnlyExpressions(this, ref _arg0);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == 3);
            Debug.Assert(variables == null || variables.Count == 0);

            return new Block3(args[0], args[1], args[2]);
        }
    }

    internal sealed class Block4 : BlockExpression {
        private object _arg0;                               // storage for the 1st argument or a ROC.  See IArgumentProvider
        private readonly Expression _arg1, _arg2, _arg3;    // storarg for the 2nd, 3rd, and 4th arguments.

        internal Block4(Expression arg0, Expression arg1, Expression arg2, Expression arg3) {
            _arg0 = arg0;
            _arg1 = arg1;
            _arg2 = arg2;
            _arg3 = arg3;
        }

        internal override Expression GetExpression(int index) {
            switch (index) {
                case 0: return ReturnObject<Expression>(_arg0);
                case 1: return _arg1;
                case 2: return _arg2;
                case 3: return _arg3;
                default: throw new InvalidOperationException();
            }
        }

        internal override int ExpressionCount {
            get {
                return 4;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnlyExpressions(this, ref _arg0);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == 4);
            Debug.Assert(variables == null || variables.Count == 0);

            return new Block4(args[0], args[1], args[2], args[3]);
        }
    }

    internal sealed class Block5 : BlockExpression {
        private object _arg0;                                       // storage for the 1st argument or a ROC.  See IArgumentProvider
        private readonly Expression _arg1, _arg2, _arg3, _arg4;     // storage for the 2nd - 5th args.

        internal Block5(Expression arg0, Expression arg1, Expression arg2, Expression arg3, Expression arg4) {
            _arg0 = arg0;
            _arg1 = arg1;
            _arg2 = arg2;
            _arg3 = arg3;
            _arg4 = arg4;
        }

        internal override Expression GetExpression(int index) {
            switch (index) {
                case 0: return ReturnObject<Expression>(_arg0);
                case 1: return _arg1;
                case 2: return _arg2;
                case 3: return _arg3;
                case 4: return _arg4;
                default: throw new InvalidOperationException();
            }
        }

        internal override int ExpressionCount {
            get {
                return 5;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnlyExpressions(this, ref _arg0);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == 5);
            Debug.Assert(variables == null || variables.Count == 0);

            return new Block5(args[0], args[1], args[2], args[3], args[4]);
        }
    }

    internal sealed class BlockN : BlockExpression {
        private IList<Expression> _expressions;         // either the original IList<Expression> or a ReadOnlyCollection if the user has accessed it.

        internal BlockN(IList<Expression> expressions) {
            Debug.Assert(expressions.Count != 0);

            _expressions = expressions;
        }

        internal override Expression GetExpression(int index) {
            Debug.Assert(index >= 0 && index < _expressions.Count);

            return _expressions[index];
        }

        internal override int ExpressionCount {
            get {
                return _expressions.Count;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnly(ref _expressions);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(variables == null || variables.Count == 0);

            return new BlockN(args);
        }
    }

    internal class ScopeExpression : BlockExpression {
        private IList<ParameterExpression> _variables;      // list of variables or ReadOnlyCollection if the user has accessed the ROC

        internal ScopeExpression(IList<ParameterExpression> variables) {
            _variables = variables;
        }

        internal override int VariableCount {
            get {
                return _variables.Count;
            }
        }

        internal override ParameterExpression GetVariable(int index) {
            return _variables[index];
        }

        internal override ReadOnlyCollection<ParameterExpression> GetOrMakeVariables() {
            return ReturnReadOnly(ref _variables);
        }

        protected IList<ParameterExpression> VariablesList {
            get {
                return _variables;
            }
        }
    }

    internal sealed class Scope1 : ScopeExpression {
        private object _body;
      
        internal Scope1(IList<ParameterExpression> variables, Expression body)
            : base(variables) {
            _body = body;
        }

        internal override Expression GetExpression(int index) {
            switch (index) {
                case 0: return ReturnObject<Expression>(_body);
                default: throw new InvalidOperationException();
            }
        }

        internal override int ExpressionCount {
            get {
                return 1;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnlyExpressions(this, ref _body);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == 1);
            Debug.Assert(variables == null || variables.Count == VariableCount);

            return new Scope1(variables ?? VariablesList, args[0]);
        }
    }

    internal sealed class ScopeN : ScopeExpression {
        private IList<Expression> _body;

        internal ScopeN(IList<ParameterExpression> variables, IList<Expression> body)
            : base(variables) {
            _body = body;
        }

        internal override Expression GetExpression(int index) {
            return _body[index];
        }

        internal override int ExpressionCount {
            get {
                return _body.Count;
            }
        }

        internal override ReadOnlyCollection<Expression> GetOrMakeExpressions() {
            return ReturnReadOnly(ref _body);
        }

        internal override BlockExpression Rewrite(IList<ParameterExpression> variables, Expression[] args) {
            Debug.Assert(args.Length == ExpressionCount);
            Debug.Assert(variables == null || variables.Count == VariableCount);

            return new ScopeN(variables ?? VariablesList, args);
        }
    }

    #endregion

    #region Block List Classes

    /// <summary>
    /// Provides a wrapper around an IArgumentProvider which exposes the argument providers
    /// members out as an IList of Expression.  This is used to avoid allocating an array
    /// which needs to be stored inside of a ReadOnlyCollection.  Instead this type has
    /// the same amount of overhead as an array without duplicating the storage of the
    /// elements.  This ensures that internally we can avoid creating and copying arrays
    /// while users of the Expression trees also don't pay a size penalty for this internal
    /// optimization.  See IArgumentProvider for more general information on the Expression
    /// tree optimizations being used here.
    /// </summary>
    internal class BlockExpressionList : IList<Expression> {
        private readonly BlockExpression _block;
        private readonly Expression _arg0;

        internal BlockExpressionList(BlockExpression provider, Expression arg0) {
            _block = provider;
            _arg0 = arg0;
        }

        #region IList<Expression> Members

        public int IndexOf(Expression item) {
            if (_arg0 == item) {
                return 0;
            }

            for (int i = 1; i < _block.ExpressionCount; i++) {
                if (_block.GetExpression(i) == item) {
                    return i;
                }
            }

            return -1;
        }

        public void Insert(int index, Expression item) {
            throw ContractUtils.Unreachable;
        }

        public void RemoveAt(int index) {
            throw ContractUtils.Unreachable;
        }

        public Expression this[int index] {
            get {
                if (index == 0) {
                    return _arg0;
                }

                return _block.GetExpression(index);
            }
            set {
                throw ContractUtils.Unreachable;
            }
        }

        #endregion

        #region ICollection<Expression> Members

        public void Add(Expression item) {
            throw ContractUtils.Unreachable;
        }

        public void Clear() {
            throw ContractUtils.Unreachable;
        }

        public bool Contains(Expression item) {
            return IndexOf(item) != -1;
        }

        public void CopyTo(Expression[] array, int arrayIndex) {
            array[arrayIndex++] = _arg0;
            for (int i = 1; i < _block.ExpressionCount; i++) {
                array[arrayIndex++] = _block.GetExpression(i);
            }
        }

        public int Count {
            get { return _block.ExpressionCount; }
        }

        public bool IsReadOnly {
            get { return true; }
        }

        public bool Remove(Expression item) {
            throw ContractUtils.Unreachable;
        }

        #endregion

        #region IEnumerable<Expression> Members

        public IEnumerator<Expression> GetEnumerator() {
            yield return _arg0;

            for (int i = 1; i < _block.ExpressionCount; i++) {
                yield return _block.GetExpression(i);
            }
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            yield return _arg0;

            for (int i = 1; i < _block.ExpressionCount; i++) {
                yield return _block.GetExpression(i);
            }
        }

        #endregion
    }

    #endregion

    public partial class Expression {

        public static BlockExpression Block(Expression arg0, Expression arg1) {
            RequiresCanRead(arg0, "arg0");
            RequiresCanRead(arg1, "arg1");
            
            return new Block2(arg0, arg1);
        }

        public static BlockExpression Block(Expression arg0, Expression arg1, Expression arg2) {
            RequiresCanRead(arg0, "arg0");
            RequiresCanRead(arg1, "arg1");
            RequiresCanRead(arg2, "arg2");
            return new Block3(arg0, arg1, arg2);
        }

        public static BlockExpression Block(Expression arg0, Expression arg1, Expression arg2, Expression arg3) {
            RequiresCanRead(arg0, "arg0");
            RequiresCanRead(arg1, "arg1");
            RequiresCanRead(arg2, "arg2");
            RequiresCanRead(arg3, "arg3");
            return new Block4(arg0, arg1, arg2, arg3);
        }

        public static BlockExpression Block(Expression arg0, Expression arg1, Expression arg2, Expression arg3, Expression arg4) {
            RequiresCanRead(arg0, "arg0");
            RequiresCanRead(arg1, "arg1");
            RequiresCanRead(arg2, "arg2");
            RequiresCanRead(arg3, "arg3");
            RequiresCanRead(arg4, "arg4");

            return new Block5(arg0, arg1, arg2, arg3, arg4);
        }

        public static BlockExpression Block(params Expression[] expressions) {
            switch (expressions.Length) {
                case 2: return Block(expressions[0], expressions[1]);
                case 3: return Block(expressions[0], expressions[1], expressions[2]);
                case 4: return Block(expressions[0], expressions[1], expressions[2], expressions[3]);
                case 5: return Block(expressions[0], expressions[1], expressions[2], expressions[3], expressions[4]);
                default:
                    ContractUtils.RequiresNotEmpty(expressions, "expressions");
                    RequiresCanRead(expressions, "expressions");
                    return new BlockN(expressions);
            }
        }

        /// <summary>
        /// Creates a list of expressions whose value is the value of the last expression.
        /// </summary>
        public static BlockExpression Block(IEnumerable<Expression> expressions) {
            return Block(EmptyReadOnlyCollection<ParameterExpression>.Instance, expressions);
        }

        public static BlockExpression Block(IEnumerable<ParameterExpression> variables, params Expression[] expressions) {
            return Block(variables, (IEnumerable<Expression>)expressions);
        }

        public static BlockExpression Block(IEnumerable<ParameterExpression> variables, IEnumerable<Expression> expressions) {
            ContractUtils.RequiresNotNull(expressions, "expressions");
            RequiresCanRead(expressions, "expressions");
            var expressionList = expressions.ToReadOnly();
            ContractUtils.RequiresNotEmpty(expressionList, "expressions");
            var varList = variables.ToReadOnly();
            ContractUtils.RequiresNotNullItems(varList, "variables");
            Expression.RequireVariablesNotByRef(varList, "variables");

            if (expressionList.Count == 1) {
                return new Scope1(varList, expressionList[0]);
            } else {
                return new ScopeN(varList, expressionList);
            }
        }
    }
}
