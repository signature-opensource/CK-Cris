using CK.TS.Angular;
using CK.TypeScript;

namespace CK.Ng.Cris.AspNet.Auth;

[TypeScriptPackage]
[NgProviderImport( "inject, APP_INITIALIZER", LibraryName = "@angular/core" )]
[NgProviderImport( "AuthService, HttpCrisEndpoint" )]
[NgProvider( """
             {
               provide: APP_INITIALIZER,
               deps: [AuthService, HttpCrisEndpoint],
               useFactory: ( a: AuthService, h: HttpCrisEndpoint ) => () => {
                 a.addOnChange( () => h.updateAmbientValuesAsync() );
               },
               multi: true
             }
            """ )]
public class TSPackage : TypeScriptPackage
{
    void StObjConstruct( CK.AspNet.Auth.TSPackage auth, CK.Ng.Cris.AspNet.TSPackage ngCris ) { }
}
