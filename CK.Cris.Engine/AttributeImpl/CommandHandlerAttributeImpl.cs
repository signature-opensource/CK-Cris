using CK.Core;
using CK.Cris;
using System;
using System.Reflection;

namespace CK.Setup.Cris;

sealed class CommandHandlerAttributeImpl : BaseHandlerAttributeImpl
{
    readonly CommandHandlerAttribute _a;

    public CommandHandlerAttributeImpl( CommandHandlerAttribute a, Type t, MethodInfo m )
        : base( t, m )
    {
        _a = a;
    }

    private protected override CSCodeGenerationResult DoImplement( IActivityMonitor monitor,
                                                                   IStObjMap engineMap,
                                                                   CrisTypeRegistry crisTypeRegistry,
                                                                   IStObjFinalClass impl,
                                                                   MethodInfo method )
    {
        return crisTypeRegistry.RegisterCommandHandler( monitor, impl!, method, _a.AllowUnclosedCommand, _a.FileName, _a.LineNumber )
                ? CSCodeGenerationResult.Success
                : CSCodeGenerationResult.Failed;
    }
}
