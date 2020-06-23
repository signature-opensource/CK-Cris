using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

namespace CK.Cris.Tests
{
    public interface IResult : IPoco
    {
        int Val { get; set; }
    }

    public interface IMoreResult : IResult
    {
        int MoreVal { get; set; }
    }

    public interface IAnotherResult : IResult
    {
        int AnotherVal { get; set; }
    }

    public interface IUnifiedResult : IMoreResult, IAnotherResult
    {
    }

    public interface ICommandWithPocoResult : ICommand<IResult> { }

    public interface ICommandWithMorePocoResult : ICommandWithPocoResult, ICommand<IMoreResult> { }

    public interface ICommandWithAnotherPocoResult : ICommandWithPocoResult, ICommand<IAnotherResult> { }

    public interface ICommandUnifiedButNotTheResult : ICommandWithMorePocoResult, ICommandWithAnotherPocoResult { }

    public interface ICommandUnifiedWithTheResult : ICommandWithMorePocoResult, ICommandWithAnotherPocoResult, ICommand<IUnifiedResult> { }

}
