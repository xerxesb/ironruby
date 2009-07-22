﻿/* ****************************************************************************
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Reflection;

using Microsoft.Scripting;
using Microsoft.Scripting.Actions;
using Microsoft.Scripting.Actions.Calls;

using IronPython.Runtime.Binding;
using IronPython.Runtime.Operations;

using Ast = System.Linq.Expressions.Expression;
using AstUtils = Microsoft.Scripting.Ast.Utils;

namespace IronPython.Runtime.Types {
    public abstract class PythonCustomTracker : CustomTracker {
        public abstract PythonTypeSlot/*!*/ GetSlot();

        public override DynamicMetaObject GetValue(OverloadResolverFactory resolverFactory, ActionBinder binder, Type type) {
            return new DynamicMetaObject(AstUtils.Constant(GetSlot(), typeof(PythonTypeSlot)), BindingRestrictions.Empty);
        }

        public override MemberTracker BindToInstance(DynamicMetaObject instance) {
            return new BoundMemberTracker(this, instance);
        }

        public override DynamicMetaObject SetValue(OverloadResolverFactory resolverFactory, ActionBinder binder, Type type, DynamicMetaObject value) {
            return SetBoundValue(resolverFactory, binder, type, value, new DynamicMetaObject(AstUtils.Constant(null), BindingRestrictions.Empty));
        }

        protected override DynamicMetaObject GetBoundValue(OverloadResolverFactory factory, ActionBinder binder, Type type, DynamicMetaObject instance) {
            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod("SlotGetValue"),
                    ((PythonOverloadResolverFactory)factory)._codeContext,
                    AstUtils.Constant(GetSlot(), typeof(PythonTypeSlot)),
                    AstUtils.Convert(
                        instance.Expression,
                        typeof(object)
                    ),
                    AstUtils.Constant(DynamicHelpers.GetPythonTypeFromType(type))
                ),
                BindingRestrictions.Empty
            );
        }
        
        protected override DynamicMetaObject SetBoundValue(OverloadResolverFactory factory, ActionBinder binder, Type type, DynamicMetaObject value, DynamicMetaObject instance) {
            return new DynamicMetaObject(
                Ast.Call(
                    typeof(PythonOps).GetMethod("SlotSetValue"),
                    ((PythonOverloadResolverFactory)factory)._codeContext,
                    AstUtils.Constant(GetSlot(), typeof(PythonTypeSlot)),
                    AstUtils.Convert(
                        instance.Expression,
                        typeof(object)
                    ),
                    AstUtils.Constant(DynamicHelpers.GetPythonTypeFromType(type)),
                    value.Expression
                ),
                BindingRestrictions.Empty
            );
        }
    }

    /// <summary>
    /// Provides a CustomTracker which handles special fields which have custom
    /// behavior on get/set.
    /// </summary>
    class CustomAttributeTracker : PythonCustomTracker {
        private readonly PythonTypeSlot/*!*/ _slot;
        private readonly Type/*!*/ _declType;
        private readonly string/*!*/ _name;

        public CustomAttributeTracker(Type/*!*/ declaringType, string/*!*/ name, PythonTypeSlot/*!*/ slot) {
            Debug.Assert(slot != null);
            Debug.Assert(declaringType != null);
            Debug.Assert(name != null);

            _declType = declaringType;
            _name = name;
            _slot = slot;
        }

        public override DynamicMetaObject GetValue(OverloadResolverFactory factory, ActionBinder binder, Type type) {
            return GetBoundValue(factory, binder, type, new DynamicMetaObject(AstUtils.Constant(null), BindingRestrictions.Empty));
        }

        public override string Name {
            get { return _name; }
        }

        public override Type DeclaringType {
            get {
                return _declType;
            }
        }

        public override PythonTypeSlot/*!*/ GetSlot() {
            return _slot;
        }
    }

    class ClassMethodTracker : PythonCustomTracker {
        private MethodTracker/*!*/[]/*!*/ _trackers;
        
        public ClassMethodTracker(MemberGroup/*!*/ group) {
            List<MethodTracker> trackers = new List<MethodTracker>(group.Count);
            
            foreach (MethodTracker mt in group) {
                trackers.Add(mt);
            }

            _trackers = trackers.ToArray();
        }

        public override PythonTypeSlot GetSlot() {
            List<MethodBase> meths = new List<MethodBase>();
            foreach (MethodTracker mt in _trackers) {
                meths.Add(mt.Method);
            }

            return PythonTypeOps.GetFinalSlotForFunction(
                PythonTypeOps.GetBuiltinFunction(DeclaringType,
                        Name,
                        meths.ToArray()
                )
            );
        }

        public override DynamicMetaObject GetValue(OverloadResolverFactory factory, ActionBinder binder, Type type) {
            return GetBoundValue(factory, binder, type, new DynamicMetaObject(AstUtils.Constant(null), BindingRestrictions.Empty));
        }

        public override Type DeclaringType {
            get { return _trackers[0].DeclaringType; }
        }

        public override string Name {
            get { return _trackers[0].Name; }
        }
    }

    class OperatorTracker : PythonCustomTracker {
        private MethodTracker/*!*/[]/*!*/ _trackers;
        private bool _reversed;
        private string/*!*/ _name;
        private Type/*!*/ _declType;

        public OperatorTracker(Type/*!*/ declaringType, string/*!*/ name, bool reversed, params MethodTracker/*!*/[]/*!*/ members) {
            Debug.Assert(declaringType != null);
            Debug.Assert(members != null);
            Debug.Assert(name != null);
            Debug.Assert(members.Length > 0);

            _declType = declaringType;
            _reversed = reversed;
            _trackers = members;
            _name = name;
        }        
        
        public override PythonTypeSlot/*!*/ GetSlot() {
            List<MethodBase> meths = new List<MethodBase>();
            foreach (MethodTracker mt in _trackers) {
                meths.Add(mt.Method);
            }

            MethodBase[] methods = meths.ToArray();
            FunctionType ft = (PythonTypeOps.GetMethodFunctionType(DeclaringType, methods) | FunctionType.Method) & (~FunctionType.Function);
            if (_reversed) {
                ft |= FunctionType.ReversedOperator;
            } else {
                ft &= ~FunctionType.ReversedOperator;
            }

            // check if this operator is only availble after importing CLR (e.g. __getitem__ on functions)
            foreach (MethodInfo mi in methods) {
                if (!mi.IsDefined(typeof(PythonHiddenAttribute), false)) {
                    ft |= FunctionType.AlwaysVisible;
                    break;
                }
            }

            return PythonTypeOps.GetFinalSlotForFunction(PythonTypeOps.GetBuiltinFunction(DeclaringType, 
                        Name,
                        ft,
                        meths.ToArray()
                    ));
        }

        public override Type DeclaringType {
            get { return _declType; }
        }

        public override string Name {
            get { return _name; }
        }
    }
}
