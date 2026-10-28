#nullable enable

using System;
using UnityEngine;
using VContainer.Unity;

namespace Kern.Core.Lifecycle
{
    [UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "Kern.Core", sourceAssembly: null, sourceClassName: "TransitionSceneLifetimeScope")]
    public abstract class TransitionSceneLifetimeScope : LifetimeScope
    {
        public Exception? AwakeFailure { get; private set; }

        protected override void Awake()
        {
            // The parent is supplied by LifetimeScope.EnqueueParent during the
            // additive load. A missing runtime parent is a hard contract error.
            try
            {
                base.Awake();
            }
            catch (Exception exception)
            {
                AwakeFailure = exception;
                throw;
            }
        }
    }
}
