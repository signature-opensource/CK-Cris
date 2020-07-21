[assembly: CK.StObj.Signature( @"fe7581659ab35ccbd9adced252abdfe0337cf6f1" )]
[assembly: CK.Setup.ExcludeFromSetup()]
#nullable disable
namespace CK
{
    using CK.Core;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using System.Text;
    using System.Reflection;
    namespace StObj
    {
        public class SignatureAttribute : Attribute
        {
            public SignatureAttribute( string s ) { }
            public readonly static (SHA1Value Signature, IReadOnlyList<string> Names) V = (SHA1Value.Parse( (string)typeof( SignatureAttribute ).Assembly.GetCustomAttributesData().First( a => a.AttributeType == typeof( SignatureAttribute ) ).ConstructorArguments[0].Value )
            , new string[] { @"" });
        }

        class GStObj : IStObj
        {
            public GStObj( Type t, IStObj g, IStObjMap m, int idx )
            {
                ClassType = t;
                Generalization = g;
                StObjMap = m;
                IndexOrdered = idx;
            }

            public Type ClassType { get; }

            public IStObj Generalization { get; }

            public IStObjMap StObjMap { get; }

            public IStObj Specialization { get; internal set; }

            public IStObjFinalImplementation FinalImplementation { get; internal set; }

            public int IndexOrdered { get; }

            internal StObjMapping AsMapping => new StObjMapping( this, FinalImplementation );
        }

        class GFinalStObj : GStObj, IStObjFinalImplementation
        {
            public GFinalStObj( object impl, Type actualType, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq, Type t, IStObj g, IStObjMap m, int idx )
                    : base( t, g, m, idx )
            {
                FinalImplementation = this;
                Implementation = impl;
                MultipleMappings = mult;
                UniqueMappings = uniq;
            }

            public object Implementation { get; }

            public Type FinalType => ClassType;

            public bool IsScoped => false;

            public IReadOnlyCollection<Type> MultipleMappings { get; }

            public IReadOnlyCollection<Type> UniqueMappings { get; }
        }

        public sealed class StObjServiceParameterInfo : IStObjServiceParameterInfo
        {
            public StObjServiceParameterInfo( Type t, int p, string n, IReadOnlyList<Type> v, bool isEnum )
            {
                ParameterType = t;
                Position = p;
                Name = n;
                Value = v;
                IsEnumerated = isEnum;
            }

            public Type ParameterType { get; }

            public int Position { get; }

            public string Name { get; }

            public bool IsEnumerated { get; }

            public IReadOnlyList<Type> Value { get; }
        }

        public sealed class StObjServiceClassDescriptor : IStObjServiceClassDescriptor
        {
            public StObjServiceClassDescriptor( Type t, Type finalType, AutoServiceKind k, IReadOnlyCollection<Type> marshallableTypes, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
            {
                ClassType = t;
                FinalType = finalType;
                AutoServiceKind = k;
                MarshallableTypes = marshallableTypes;
                MultipleMappings = mult;
                UniqueMappings = uniq;
            }

            public Type ClassType { get; }

            public Type FinalType { get; }

            public bool IsScoped => (AutoServiceKind & AutoServiceKind.IsScoped) != 0;

            public AutoServiceKind AutoServiceKind { get; }

            public IReadOnlyCollection<Type> MarshallableTypes { get; }

            public IReadOnlyCollection<Type> MultipleMappings { get; }

            public IReadOnlyCollection<Type> UniqueMappings { get; }
        }

        public sealed class StObjServiceClassFactoryInfo : IStObjServiceClassFactoryInfo
        {
            public StObjServiceClassFactoryInfo( Type t, Type finalType, IReadOnlyList<IStObjServiceParameterInfo> a, AutoServiceKind k, IReadOnlyCollection<Type> marshallableTypes, IReadOnlyCollection<Type> mult, IReadOnlyCollection<Type> uniq )
            {
                ClassType = t;
                FinalType = finalType;
                Assignments = a;
                AutoServiceKind = k;
                MarshallableTypes = marshallableTypes;
                MultipleMappings = mult;
                UniqueMappings = uniq;
            }

            public Type ClassType { get; }

            public Type FinalType { get; }

            public bool IsScoped => (AutoServiceKind & AutoServiceKind.IsScoped) != 0;

            public AutoServiceKind AutoServiceKind { get; }

            public IReadOnlyList<IStObjServiceParameterInfo> Assignments { get; }

            public IReadOnlyCollection<Type> MarshallableTypes { get; }

            public IReadOnlyCollection<Type> MultipleMappings { get; }

            public IReadOnlyCollection<Type> UniqueMappings { get; }
        }

        public sealed class GeneratedRootContext : IStObjMap, IStObjObjectMap, IStObjServiceMap
        {
            readonly GStObj[] _stObjs;
            readonly GFinalStObj[] _finalStObjs;
            readonly Dictionary<Type, GFinalStObj> _map;


            public IStObjObjectMap StObjs => this;

            IReadOnlyList<string> IStObjMap.Names => CK.StObj.SignatureAttribute.V.Names;
            SHA1Value IStObjMap.GeneratedSignature => CK.StObj.SignatureAttribute.V.Signature;

            IStObj IStObjObjectMap.ToLeaf( Type t ) => GToLeaf( t );
            object IStObjObjectMap.Obtain( Type t ) => _map.TryGetValue( t, out var s ) ? s.Implementation : null;

            IEnumerable<IStObjFinalImplementation> IStObjObjectMap.FinalImplementations => _finalStObjs;

            IEnumerable<StObjMapping> IStObjObjectMap.StObjs => _stObjs.Select( s => s.AsMapping );

            GFinalStObj GToLeaf( Type t ) => _map.TryGetValue( t, out var s ) ? s : null;

            readonly Dictionary<Type, IStObjFinalImplementation> _objectServiceMappings;
            readonly IStObjFinalImplementation[] _objectServiceMappingList;
            readonly Dictionary<Type, IStObjServiceClassDescriptor> _simpleServiceMappings;
            readonly IStObjServiceClassDescriptor[] _simpleServiceList;
            readonly Dictionary<Type, IStObjServiceClassFactory> _manualServiceMappings;
            readonly IStObjServiceClassFactory[] _manualServiceList;

            public IStObjServiceMap Services => this;
            IReadOnlyDictionary<Type, IStObjFinalImplementation> IStObjServiceMap.ObjectMappings => _objectServiceMappings;
            IReadOnlyList<IStObjFinalImplementation> IStObjServiceMap.ObjectMappingList => _objectServiceMappingList;
            IReadOnlyDictionary<Type, IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappings => _simpleServiceMappings;
            IReadOnlyList<IStObjServiceClassDescriptor> IStObjServiceMap.SimpleMappingList => _simpleServiceList;
            IReadOnlyDictionary<Type, IStObjServiceClassFactory> IStObjServiceMap.ManualMappings => _manualServiceMappings;
            IReadOnlyList<IStObjServiceClassFactory> IStObjServiceMap.ManualMappingList => _manualServiceList;
            readonly IReadOnlyCollection<VFeature> _vFeatures;
            public IReadOnlyCollection<VFeature> Features => _vFeatures;
            public GeneratedRootContext( IActivityMonitor monitor )
            {
                _stObjs = new GStObj[2];
                _finalStObjs = new GFinalStObj[2];
                _stObjs[0] = _finalStObjs[0] = new GFinalStObj( new CK.Core.PocoDirectory_CK(), typeof( CK.Core.PocoDirectory_CK ),
                Type.EmptyTypes,
                Type.EmptyTypes,
                typeof( CK.Core.PocoDirectory ), null, this, 0 );
                _stObjs[1] = _finalStObjs[1] = new GFinalStObj( new CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK(), typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK ),
                Type.EmptyTypes,
                new[] { typeof( CK.Core.IPocoFactory<CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand> ) },
                typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK ), null, this, 1 );
                _map = new Dictionary<Type, GFinalStObj>();
                _map.Add( typeof( CK.Core.PocoDirectory ), (GFinalStObj)_stObjs[0] );
                _map.Add( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK ), (GFinalStObj)_stObjs[1] );
                _map.Add( typeof( CK.Core.IPocoFactory<CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand> ), (GFinalStObj)_stObjs[1] );
                int iStObj = 2;
                while( --iStObj >= 0 )
                {
                    var o = _stObjs[iStObj];
                    if( o.Specialization == null )
                    {
                        var oF = (GFinalStObj)o;
                        GStObj g = (GStObj)o.Generalization;
                        while( g != null )
                        {
                            g.Specialization = o;
                            g.FinalImplementation = oF;
                            o = g;
                            g = (GStObj)o.Generalization;
                        }
                    }
                }
                _objectServiceMappings = new Dictionary<Type, IStObjFinalImplementation>( 0 );
                _objectServiceMappingList = Array.Empty<IStObjFinalImplementation>();
                _simpleServiceList = new IStObjServiceClassDescriptor[5];
                _simpleServiceList[0] = new StObjServiceClassDescriptor( typeof( CK.Cris.FrontCommandExecutor ), typeof( CK.Cris.FrontCommandExecutor_CK ), ((CK.Core.AutoServiceKind)8L), Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes );
                _simpleServiceList[1] = new StObjServiceClassDescriptor( typeof( CK.Cris.CommandDirectory ), typeof( CK.Cris.CommandDirectory_CK ), ((CK.Core.AutoServiceKind)8L), Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes );
                _simpleServiceList[2] = new StObjServiceClassDescriptor( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService ), typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService ), ((CK.Core.AutoServiceKind)8L), Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes );
                _simpleServiceList[3] = new StObjServiceClassDescriptor( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AuthService ), typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AuthService ), ((CK.Core.AutoServiceKind)8L), Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes );
                _simpleServiceList[4] = new StObjServiceClassDescriptor( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.SecurityService ), typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.SecurityService ), ((CK.Core.AutoServiceKind)8L), Type.EmptyTypes, Type.EmptyTypes, Type.EmptyTypes );
                _simpleServiceMappings = new Dictionary<Type, IStObjServiceClassDescriptor>( 5 );
                _simpleServiceMappings.Add( typeof( CK.Cris.FrontCommandExecutor ), _simpleServiceList[0] );
                _simpleServiceMappings.Add( typeof( CK.Cris.CommandDirectory ), _simpleServiceList[1] );
                _simpleServiceMappings.Add( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService ), _simpleServiceList[2] );
                _simpleServiceMappings.Add( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AuthService ), _simpleServiceList[3] );
                _simpleServiceMappings.Add( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.SecurityService ), _simpleServiceList[4] );
                _manualServiceList = new IStObjServiceClassFactory[0];
                _manualServiceMappings = new Dictionary<Type, IStObjServiceClassFactory>( 0 );
                _vFeatures = new VFeature[] { new VFeature( @"CK.Cris", CSemVer.SVersion.Parse( @"0.0.0-0" ) ), new VFeature( @"CK.Cris.Executor", CSemVer.SVersion.Parse( @"0.0.0-0" ) ), new VFeature( @"CK.Cris.Executor.Tests", CSemVer.SVersion.Parse( @"0.0.0-0" ) ), new VFeature( @"CK.StObj.Model", CSemVer.SVersion.Parse( @"0.0.0--02wak5d-local+v14.0.1" ) ) };
            }
            void IStObjObjectMap.ConfigureServices( in StObjContextRoot.ServiceRegister register )
            {
                register.StartupServices.Add( typeof( IStObjObjectMap ), this );
                object[] registerParam = new object[] { register.Monitor, register.StartupServices };
            }
        }
        static public class SFInfo
        {
        }
    }
    namespace Core
    {
        public class PocoDirectory_CK : CK.Core.PocoDirectory
        {
            internal static PocoDirectory_CK Instance;
            static readonly Dictionary<string, IPocoFactory> _factories = new Dictionary<string, IPocoFactory>( 1 );
            public override IPocoFactory Find( string name ) => _factories.GetValueOrDefault( name );
            internal static void Register( IPocoFactory f )
            {
                _factories.Add( f.Name, f );
                foreach( var n in f.PreviousNames ) _factories.Add( n, f );

            }
            public PocoDirectory_CK()
            {
                Instance = this;
            }
        }
    }
    namespace Cris
    {
        namespace Front
        {
            namespace AspNet
            {
                namespace Tests
                {
                    public sealed class FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK : CK.Core.IPocoFactory<CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand>, CK.Cris.ICommandModel
                    {
                        PocoDirectory IPocoFactory.PocoDirectory => PocoDirectory_CK.Instance;
                        public Type PocoClassType => typeof( FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK );
                        public IPoco Create() => new FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK();
                        public string Name => @"CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests+IAmbientValuesCollectCommand";
                        public IReadOnlyList<string> PreviousNames => Array.Empty<string>();
                        CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand CK.Core.IPocoFactory<CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand>.Create() => new CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK();
                        public Type CommandType => PocoClassType;
                        public int CommandIdx => 0;
                        public string CommandName => Name;
                        public Type ResultType => typeof( System.Collections.Generic.Dictionary<string, object> );
                        public MethodInfo Handler => (System.Reflection.MethodInfo)typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService ).GetMembers( System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.DeclaredOnly )[0];
                        CK.Cris.ICommand CK.Cris.ICommandModel.Create() => (CK.Cris.ICommand)Create();
                        public FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK()
                        {
                            PocoDirectory_CK.Register( this );
                            FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK._factory = this;
                        }
                    }
                    public sealed class FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK : IPocoClass, CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand
                    {
                        internal static FrontCommandExecutorTests_IAmbientValuesCollectCommand_CKFactory_CK _factory;
                        public IPocoFactory Factory => _factory;
                        public CK.Cris.ICommandModel CommandModel => _factory;
                        public FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK()
                        {
                        }
                    }
                }
            }
        }
        public class FrontCommandExecutor_CK : CK.Cris.FrontCommandExecutor
        {
            static async Task<object> H0( IActivityMonitor m, IServiceProvider s, CK.Cris.ICommand c )
            {
                var handler = (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService)s.GetService( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AmbientValuesService ) );
                System.Collections.Generic.Dictionary<string, object> r = handler.GetValues( (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand)c );
                {
                    var h = (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AuthService)s.GetService( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.AuthService ) );
                    h.GetValues( (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand)c, (CK.Auth.IAuthenticationInfo)s.GetService( typeof( CK.Auth.IAuthenticationInfo ) ), r );
                }
                {
                    var h = (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.SecurityService)s.GetService( typeof( CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.SecurityService ) );
                    await h.GetValuesAsync( (CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests.IAmbientValuesCollectCommand)c, m, (CK.Auth.IAuthenticationInfo)s.GetService( typeof( CK.Auth.IAuthenticationInfo ) ), r );
                }
                return (object)r;
            }
            static readonly Func<IActivityMonitor, IServiceProvider, CK.Cris.ICommand, Task<object>> NoHandler = ( m, s, c ) => throw new Exception( "No Command handler found." );
            readonly Func<IActivityMonitor, IServiceProvider, CK.Cris.ICommand, Task<object>>[] _handlers = new Func<IActivityMonitor, IServiceProvider, CK.Cris.ICommand, Task<object>>[1] { H0 };
            public FrontCommandExecutor_CK( CK.Cris.CommandDirectory directory ) : base( directory )
            {
            }
            protected override Task<object> DoExecuteCommandAsync( IActivityMonitor m, IServiceProvider s, CK.Cris.ICommand c )
            {
                return _handlers[c.CommandModel.CommandIdx]( m, s, c );
            }
        }
        public class CommandDirectory_CK : CK.Cris.CommandDirectory
        {
            public CommandDirectory_CK() : base( CreateCommands() ) { }
            static IReadOnlyList<CK.Cris.ICommandModel> CreateCommands()

            {
                var list = new ICommandModel[]
                {
CK.Cris.Front.AspNet.Tests.FrontCommandExecutorTests_IAmbientValuesCollectCommand_CK._factory,
                };
                return list;
            }
        }
    }
}
