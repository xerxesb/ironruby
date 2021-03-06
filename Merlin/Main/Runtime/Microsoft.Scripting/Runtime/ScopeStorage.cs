﻿/* ****************************************************************************
*
* Copyright (c) Microsoft Corporation. 
*
* This source code is subject to terms and conditions of the Microsoft Public License. A 
* copy of the license can be found in the License.html file at the root of this distribution. If 
* you cannot locate the Microsoft Public License, please send an email to 
* dlr@microsoft.com. By using this source code in any fashion, you are agreeing to be bound 
* by the terms of the Microsoft Public License.
*
* You must not remove this notice, or any other, from this software.
*
*
* ***************************************************************************/

#if CLR2
using dynamic = System.Object;
using Microsoft.Scripting.Ast;
#else
using System.Linq.Expressions;
#endif

using System;
using System.Collections.Generic;
using System.Threading;
using System.Dynamic;

namespace Microsoft.Scripting {
    /// <summary>
    /// Provides optimized and cachable support for scope storage.
    /// 
    /// This is the default object used for storing values in a scope.
    /// 
    /// </summary>
    /// <remarks>
    /// The implementation uses a case-insensitive dictionary which holds
    /// onto ScopeVariableIgnoreCase objects.  The SVIC's hold onto ScopeVariable
    /// objects for each possible casing.
    /// </remarks>
    public sealed class ScopeStorage : IDynamicMetaObjectProvider {
        private readonly Dictionary<string, ScopeVariableIgnoreCase> _storage = new Dictionary<string, ScopeVariableIgnoreCase>(StringComparer.OrdinalIgnoreCase);
        
        /// <summary>
        /// Gets the named value from the scope optionally ignoring case.
        /// 
        /// If the named value is not present an InvalidOperationException is raised.
        /// </summary>
        public dynamic GetValue(string name, bool ignoreCase) {
            object res;
            if (GetVariable(name, ignoreCase).TryGetValue(out res)) {
                return res;
            }
            throw new KeyNotFoundException("no value");
        }

        /// <summary>
        /// Attempts to get the named value from the scope optionally ignoring the case.
        /// 
        /// Returns true if the value is present, false if it is not.
        /// </summary>
        public bool TryGetValue(string name, bool ignoreCase, out dynamic value) {
            if (HasVariable(name)) {
                object objValue;
                if (GetVariable(name, ignoreCase).TryGetValue(out objValue)) {
                    value = objValue;
                    return true;
                }
            }

            value = null;
            return false;
        }

        /// <summary>
        /// Sets the named value in the scope optionally ignoring the case.
        /// </summary>
        public void SetValue(string name, bool ignoreCase, object value) {
            GetVariable(name, ignoreCase).SetValue(value);
        }

        /// <summary>
        /// Deletes the named value from the scope optionally ignoring the case.
        /// </summary>
        public bool DeleteValue(string name, bool ignoreCase) {
            if (!HasVariable(name)) {
                return false;
            }
            return GetVariable(name, ignoreCase).DeleteValue();
        }

        /// <summary>
        /// Checks if the named value is present in the scope optionally ignoring the case.
        /// </summary>
        public bool HasValue(string name, bool ignoreCase) {
            if (!HasVariable(name)) {
                return false;
            }
            return GetVariable(name, ignoreCase).HasValue;
        }

        /// <summary>
        /// Gets the IScopeVariable for the scope optionally ignoring case.
        /// 
        /// The IScopeVariable can be held onto and get/set/deleted without performing
        /// a dictionary lookup on subsequent accesses.
        /// </summary>
        public IScopeVariable GetVariable(string name, bool ignoreCase) {
            if (ignoreCase) {
                return GetVariableIgnoreCase(name);
            }
            return GetVariable(name);
        }

        /// <summary>
        /// Gets the ScopeVariable for the scope in a case-sensitive manner.
        /// 
        /// The ScopeVariable can be held onto and get/set/deleted without performing
        /// a dictionary lookup on subsequent accesses.
        /// </summary>
        public ScopeVariable GetVariable(string name) {
            return GetVariableIgnoreCase(name).GetCaseSensitiveStorage(name);
        }

        /// <summary>
        /// Gets the ScopeVariableIgnoreCase for the scope in a case-insensitive manner.
        /// 
        /// The ScopeVariable can be held onto and get/set/deleted without performing
        /// a dictionary lookup on subsequent accesses.
        /// </summary>
        public ScopeVariableIgnoreCase GetVariableIgnoreCase(string name) {
            ScopeVariableIgnoreCase storageInfo;
            lock (_storage) {
                if (!_storage.TryGetValue(name, out storageInfo)) {
                    _storage[name] = storageInfo = new ScopeVariableIgnoreCase(name);
                }
                return storageInfo;
            }
        }
        /// <summary>
        /// Provides convenient case-sensitive value access.
        /// </summary>
        public dynamic this[string index] {
            get {
                return GetValue(index, false);
            }
            set {
                SetValue(index, false, (object)value);
            }
        }

        /// <summary>
        /// Returns all of the member names which currently have values in the scope.
        /// 
        /// The list contains all available casings.
        /// </summary>
        public IList<string> GetMemberNames() {
            List<string> res = new List<string>();
            lock (_storage) {
                foreach (var storage in _storage.Values) {
                    storage.AddNames(res);
                }
            }
            return res;
        }

        /// <summary>
        /// Returns all of the member names and their associated values from the scope.
        /// 
        /// The list contains all available casings.
        /// </summary>
        public IList<KeyValuePair<string, object>> GetItems() {
            List<KeyValuePair<string, object>> res = new List<KeyValuePair<string, object>>();
            lock (_storage) {
                foreach (var storage in _storage.Values) {
                    storage.AddItems(res);
                }
            }
            return res;
        }

        private bool HasVariable(string name) {
            lock (_storage) {
                return _storage.ContainsKey(name);
            }
        }

        #region IDynamicMetaObjectProvider Members

        public DynamicMetaObject GetMetaObject(Expression parameter) {
            return new Meta(parameter, this);
        }

        class Meta : DynamicMetaObject {
            public Meta(Expression parameter, ScopeStorage storage)
                : base(parameter, BindingRestrictions.Empty, storage) {
            }

            public override DynamicMetaObject BindGetMember(GetMemberBinder binder) {
                IScopeVariable variable = Value.GetVariable(binder.Name, binder.IgnoreCase);
                var tmp = Expression.Parameter(typeof(object));
                return new DynamicMetaObject(
                    Expression.Block(
                        new[] { tmp },
                        Expression.Condition(
                            Expression.Call(
                                Expression.Constant(variable),
                                variable.GetType().GetMethod("TryGetValue"),
                                tmp
                            ),
                            tmp,
                            binder.FallbackGetMember(this).Expression
                        )
                    ),
                    BindingRestrictions.GetInstanceRestriction(Expression, Value)
                );
            }

            public override DynamicMetaObject BindSetMember(SetMemberBinder binder, DynamicMetaObject value) {
                IScopeVariable variable = Value.GetVariable(binder.Name, binder.IgnoreCase);

                var objExpression = Expression.Convert(value.Expression, typeof(object));
                return new DynamicMetaObject(
                    Expression.Block(
                        Expression.Call(
                            Expression.Constant(variable),
                            variable.GetType().GetMethod("SetValue"),
                            objExpression
                        ),
                        objExpression
                    ),
                    BindingRestrictions.GetInstanceRestriction(Expression, Value)
                );
            }

            public override DynamicMetaObject BindDeleteMember(DeleteMemberBinder binder) {
                IScopeVariable variable = Value.GetVariable(binder.Name, binder.IgnoreCase);
                return new DynamicMetaObject(
                    Expression.Condition(
                        Expression.Call(
                            Expression.Constant(variable),
                            variable.GetType().GetMethod("DeleteValue")
                        ),
                        Expression.Default(binder.ReturnType),
                        binder.FallbackDeleteMember(this).Expression
                    ),
                    BindingRestrictions.GetInstanceRestriction(Expression, Value)
                );
            }

            public override IEnumerable<string> GetDynamicMemberNames() {
                return Value.GetMemberNames();
            }

            public new ScopeStorage Value {
                get {
                    return (ScopeStorage)base.Value;
                }
            }
        }

        #endregion

    }

    /// <summary>
    /// Provides a common interface for accessing both case sensitive and 
    /// case insensitive variable storage.
    /// </summary>
    public interface IScopeVariable {
        /// <summary>
        /// True if the scope has a value, false if it does not.
        /// </summary>
        bool HasValue {
            get;
        }
        
        /// <summary>
        /// Atempts to get the value. If a value is assigned it returns true otherwise
        /// it returns false.
        /// </summary>
        bool TryGetValue(out dynamic value);
        
        /// <summary>
        /// Sets the current value in the scope.
        /// </summary>
        void SetValue(object value);
        
        /// <summary>
        /// Removes the current value from the scope.
        /// </summary>
        bool DeleteValue();
    }

    /// <summary>
    /// Boxes the value for storage in a scope. Languages or consumers of the scope
    /// can save this value and use it to get/set the current value in the scope for
    /// commonly accessed values.
    /// 
    /// ScopeVariables are case sensitive and will only refer to a single value.
    /// </summary>
    public sealed class ScopeVariable : IScopeVariable {
        private object _value;
        private static readonly object _novalue = new object();
        
        internal ScopeVariable() {
            _value = _novalue;
        }

        #region Public APIs

        /// <summary>
        /// True if the scope has a value, false if it does not.
        /// </summary>
        public bool HasValue {
            get {
                return _value != _novalue;
            }
        }

        /// <summary>
        /// Atempts to get the value. If a value is assigned it returns true otherwise
        /// it returns false.
        /// </summary>
        public bool TryGetValue(out dynamic value) {
            value = _value;
            if ((object)value != _novalue) {
                return true;
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Sets the current value in the scope.
        /// </summary>
        public void SetValue(object value) {
            _value = value;
        }

        /// <summary>
        /// Removes the current value from the scope.
        /// </summary>
        public bool DeleteValue() {
            return Interlocked.Exchange(ref _value, _novalue) != _novalue;
        }

        #endregion
    }

    /// <summary>
    /// Boxes the value for storage in a scope. Languages or consumers of the scope
    /// can save this value and use it to get/set the current value in the scope for
    /// commonly accessed values.
    /// 
    /// ScopeVariablesIgnoreCase are case insensitive and may access different casings
    /// depending on how other gets/sets occur in the scope.
    /// </summary>
    public sealed class ScopeVariableIgnoreCase : IScopeVariable {
        private readonly string _firstCasing;
        private readonly ScopeVariable _firstVariable;
        private Dictionary<string, ScopeVariable> _overflow;
        
        internal ScopeVariableIgnoreCase(string casing) {
            _firstCasing = casing;
            _firstVariable = new ScopeVariable();
        }

        #region Public APIs

        /// <summary>
        /// True if the scope has a value, false if it does not.
        /// </summary>
        public bool HasValue {
            get {
                if (_firstVariable.HasValue) {
                    return true;
                }
                if (_overflow != null) {
                    lock (_overflow) {
                        foreach (var entry in _overflow) {
                            if (entry.Value.HasValue) {
                                return true;
                            }
                        }
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Atempts to get the value. If a value is assigned it returns true otherwise
        /// it returns false.
        /// </summary>
        public bool TryGetValue(out dynamic value) {
            object objValue;
            if (_firstVariable.TryGetValue(out objValue)) {
                value = objValue;
                return true;
            }
            if (_overflow != null) {
                lock (_overflow) {
                    foreach (var entry in _overflow) {
                        if (entry.Value.TryGetValue(out objValue)) {
                            value = objValue;
                            return true;
                        }
                    }
                }
            }
            value = null;
            return false;
        }

        /// <summary>
        /// Sets the current value in the scope.
        /// </summary>
        public void SetValue(object value) {
            _firstVariable.SetValue(value);
        }

        /// <summary>
        /// Removes the current value from the scope.
        /// </summary>
        public bool DeleteValue() {
            bool res = _firstVariable.DeleteValue();
            if (_overflow != null) {
                lock (_overflow) {
                    foreach (var entry in _overflow) {
                        res = entry.Value.DeleteValue() || res;
                    }
                }
            }
            return res;
        }

        #endregion

        #region Implementation Details

        internal ScopeVariable GetCaseSensitiveStorage(string name) {
            if (name == _firstCasing) {
                // common case, only 1 casing available
                return _firstVariable;
            }
            return GetStorageSlow(name);
        }

        internal void AddNames(List<string> list) {
            if (_firstVariable.HasValue) {
                list.Add(_firstCasing);
            }

            if (_overflow != null) {
                lock (_overflow) {
                    foreach (var element in _overflow) {
                        if (element.Value.HasValue) {
                            list.Add(element.Key);
                        }
                    }
                }
            }

        }

        internal void AddItems(List<KeyValuePair<string, object>> list) {
            object value;
            if (_firstVariable.TryGetValue(out value)) {
                list.Add(new KeyValuePair<string, object>(_firstCasing, value));
            }

            if (_overflow != null) {
                lock (_overflow) {
                    foreach (var element in _overflow) {
                        if (element.Value.TryGetValue(out value)) {
                            list.Add(new KeyValuePair<string, object>(element.Key, value));
                        }
                    }
                }
            }

        }

        private ScopeVariable GetStorageSlow(string name) {
            if (_overflow == null) {
                Interlocked.CompareExchange(ref _overflow, new Dictionary<string, ScopeVariable>(), null);
            }

            lock (_overflow) {
                ScopeVariable res;
                if (!_overflow.TryGetValue(name, out res)) {
                    _overflow[name] = res = new ScopeVariable();
                }
                return res;
            }
        }

        #endregion
    }
}
