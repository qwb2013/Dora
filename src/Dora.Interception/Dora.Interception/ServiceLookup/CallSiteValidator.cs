// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Dora.Interception.Properties;
using System;
using System.Collections.Generic;
using Dora.Interception;

namespace Microsoft.Extensions.DependencyInjection.ServiceLookup
{
    internal class CallSiteValidator: CallSiteVisitor<CallSiteValidator.CallSiteValidatorState, Type>
    {
        // Keys are services being resolved via GetService, values - first scoped service in their call site tree
        private readonly Dictionary<Type, Type> _scopedServices = new Dictionary<Type, Type>();

        public void ValidateCallSite(Type serviceType, IServiceCallSite callSite)
        {
            var scoped = VisitCallSite(callSite, default(CallSiteValidatorState));
            if (scoped != null)
            {
                _scopedServices.Add(serviceType, scoped);
            }
        }

        public void ValidateResolution(Type serviceType, ServiceProvider serviceProvider)
        {
            Type scopedService;
            if (ReferenceEquals(serviceProvider, serviceProvider.Root)
                && _scopedServices.TryGetValue(serviceType, out scopedService))
            {
                if (serviceType == scopedService)
                {
                    throw new InvalidOperationException(
                        Resources.DirectScopedResolvedFromRootException.Fill(serviceType,
                            nameof(ServiceLifetime.Scoped).ToLowerInvariant()));
                }

                throw new InvalidOperationException(
                    Resources.ScopedResolvedFromRootException.Fill(
                        serviceType,
                        scopedService,
                        nameof(ServiceLifetime.Scoped).ToLowerInvariant()));
            }
        }

        protected override Type VisitTransient(TransientCallSite transientCallSite, CallSiteValidatorState state)
        {
            return VisitCallSite(transientCallSite.Service, state);
        }

        protected override Type VisitConstructor(ConstructorCallSite constructorCallSite, CallSiteValidatorState state)
        {
            Type result = null;
            foreach (var parameterCallSite in constructorCallSite.ParameterCallSites)
            {
                var scoped =  VisitCallSite(parameterCallSite, state);
                if (result == null)
                {
                    result = scoped;
                }
            }
            return result;
        }

        protected override Type VisitClosedIEnumerable(ClosedIEnumerableCallSite closedIEnumerableCallSite,
            CallSiteValidatorState state)
        {
            Type result = null;
            foreach (var serviceCallSite in closedIEnumerableCallSite.ServiceCallSites)
            {
                var scoped = VisitCallSite(serviceCallSite, state);
                if (result == null)
                {
                    result = scoped;
                }
            }
            return result;
        }

        protected override Type VisitSingleton(SingletonCallSite singletonCallSite, CallSiteValidatorState state)
        {
            state.Singleton = singletonCallSite;
            return VisitCallSite(singletonCallSite.ServiceCallSite, state);
        }

        protected override Type VisitScoped(ScopedCallSite scopedCallSite, CallSiteValidatorState state)
        {
            // We are fine with having ServiceScopeService requested by singletons
            if (scopedCallSite.ServiceCallSite is ServiceScopeService)
            {
                return null;
            }
            if (state.Singleton != null)
            {
                throw new InvalidOperationException(Resources.ScopedInSingletonException.Fill(
                    scopedCallSite.Key.ServiceType,
                    state.Singleton.Key.ServiceType,
                    nameof(ServiceLifetime.Scoped).ToLowerInvariant(),
                    nameof(ServiceLifetime.Singleton).ToLowerInvariant()
                    ));
            }
            return scopedCallSite.Key.ServiceType;
        }

        protected override Type VisitConstant(ConstantCallSite constantCallSite, CallSiteValidatorState state) => null;

        protected override Type VisitCreateInstance(CreateInstanceCallSite createInstanceCallSite, CallSiteValidatorState state) => null;

        protected override Type VisitInstanceService(InstanceService instanceCallSite, CallSiteValidatorState state) => null;

        protected override Type VisitServiceProviderService(ServiceProviderService serviceProviderService, CallSiteValidatorState state) => null;

        protected override Type VisitEmptyIEnumerable(EmptyIEnumerableCallSite emptyIEnumerableCallSite, CallSiteValidatorState state) => null;

        protected override Type VisitServiceScopeService(ServiceScopeService serviceScopeService, CallSiteValidatorState state) => null;

        protected override Type VisitFactoryService(FactoryService factoryService, CallSiteValidatorState state) => null;

        protected override Type VisitInterception(IntercepCallSite interceptCallSite, CallSiteValidatorState argument) => null;

        internal struct CallSiteValidatorState
        {
            public SingletonCallSite Singleton { get; set; }
        }
    }
}