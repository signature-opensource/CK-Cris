using CK.Core;
using System;
using System.Collections.Generic;
using System.Text;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace CK.Cris.Tests
{
    public interface IResult : IPoco
    {
        int Val { get; set; }
    }

    /// <summary>
    /// Extends the basic result with a <see cref="MoreVal"/>.
    /// </summary>
    public interface IMoreResult : IResult
    {
        /// <summary>
        /// Gets or sets the More value.
        /// </summary>
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

    // Cannot work: the results are NOT unified in a final type.
    public interface ICommandUnifiedButNotTheResult : ICommandWithMorePocoResult, ICommandWithAnotherPocoResult { }

    public interface ICommandUnifiedWithTheResult : ICommandWithMorePocoResult, ICommandWithAnotherPocoResult, ICommand<IUnifiedResult> { }

}
