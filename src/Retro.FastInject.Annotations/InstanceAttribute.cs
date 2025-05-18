using System;

namespace Retro.FastInject.Annotations;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field)]
public class InstanceAttribute : Attribute;