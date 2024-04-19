// This will run once before each test file and before the testing framework is installed.
// This is used by TestHelper.CreateTypeScriptTestRunner to duplicate environment variables settings
// in a "persistent" way: these environment variables will be available until the TypeScriptRunner
// returned by CreateTypeScriptTestRunner is disposed.
Object.assign( process.env, {"CRIS_ENDPOINT_URL":"http://[::1]:52835/.cris","STOBJ_TYPESCRIPT_ENGINE":"true"});
