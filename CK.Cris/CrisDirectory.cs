using CK.Core;
using System;
using System.Collections.Generic;

namespace CK.Cris
{
    /// <summary>
    /// Directory that contains all the available events and commands in the context.
    /// </summary>
    [CK.Setup.ContextBoundDelegation( "CK.Setup.Cris.CrisDirectoryImpl, CK.Cris.Engine" )]
    public abstract class CrisDirectory : ISingletonAutoService
    {
        /// <summary>
        /// Gets the "Cris" tag.
        /// </summary>
        public readonly static CKTrait CrisTag = ActivityMonitor.Tags.Register( "Cris" );

        protected CrisDirectory( IReadOnlyList<ICrisPocoModel> models )
        {
            CrisPocoModels = models;
        }

        /// <summary>
        /// Gets all the commands indexed by their <see cref="ICrisPocoModel.CrisPocoIndex"/>.
        /// </summary>
        public IReadOnlyList<ICrisPocoModel> CrisPocoModels { get; }



    }
}
