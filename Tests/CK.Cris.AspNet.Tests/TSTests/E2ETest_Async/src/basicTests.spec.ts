import axios from "axios"; 
import { HttpCrisEndpoint, AmbientValues, CrisError, UserMessageLevel, SimpleUserMessage } from "@local/ck-gen"; 
import { BeautifulCommand, BuggyCommand, CommandWithMessage } from "@local/ck-gen"; 
import { type } from "os";

const crisEndpoint = process.env.CRIS_ENDPOINT_URL ?? "";
const withEndpoint = crisEndpoint ? it : it.skip;

it('isConnect is false until the first command is sent', async () => {
  const ep = new HttpCrisEndpoint( axios, crisEndpoint );
  expect( ep.isConnected ).toBeFalsy();
  await ep.sendAsync( BeautifulCommand.create( "Gorgeous" ) );
  expect( ep.isConnected ).toBeTruthy();
});

it( 'default ambient color is red.', async () =>
{
  const ep = new HttpCrisEndpoint( axios, crisEndpoint );
  // ambient values are not exposed since they are refreshable: 
  // hiding them avoids a potentially stale data.
  // An explicit update is required.
  const a = await ep.updateAmbientValuesAsync();
  expect( a.color ).toBe('Red');
});

it( 'ambient values are set on sent command.', async () => 
{
  const ep = new HttpCrisEndpoint( axios, crisEndpoint );
  const cmd = BeautifulCommand.create("Gorgeous");
  // This should be the empty string since Color is NOT nullable... 
  expect( cmd.color ).toBeUndefined();
  // The BeatifulCommand handler returns a string that is "Color - Beauty".
  const executedCommand = await ep.sendAsync( cmd );
  expect( cmd.color ).toBe( "Red" );
  expect( executedCommand.result ).toBe( "Red - Gorgeous" );
});

it( 'ambient values can be overridden.', async () => 
{
  const ep = new HttpCrisEndpoint( axios, crisEndpoint );
  const cmd = BeautifulCommand.create("Superb");
  ep.ambientValuesOverride.color = "Black";
  // This should be the empty string since Color is NOT nullable... 
  expect( cmd.color ).toBeUndefined();
  // The BeatifulCommand handler returns a string that is "Color - Beauty".
  const executedCommand = await ep.sendAsync( cmd );
  expect( cmd.color ).toBe( "Black" );
  expect( executedCommand.result ).toBe( "Black - Superb" );
});

it('sendOrThrowAsync throws the CrisError.', async () => {
    const ep = new HttpCrisEndpoint(axios, crisEndpoint);
    const cmd = BuggyCommand.create(true);
    try {
        await ep.sendOrThrowAsync(cmd);
        fail("Never here!");
    }
    catch (ex) {
        expect(ex instanceof CrisError);
        const cex = <CrisError>ex;
        expect(cex.errorType === "ValidationError");
    }
    cmd.emitValidationError = false;
    try {
        await ep.sendOrThrowAsync(cmd);
        fail("Never here!");
    }
    catch (ex) {
        expect(ex instanceof CrisError);
        const cex = <CrisError>ex;
        expect(cex.errorType === "ExecutionError");
    }
});
it('CrisError validation messages are SimpleMessage.', async () => {
    const ep = new HttpCrisEndpoint(axios, crisEndpoint);
    const cmd = BuggyCommand.create(true);
    var executed = await ep.sendAsync(cmd);
    expect(executed.result instanceof CrisError );
    const r = <CrisError>executed.result;
    expect( r.errorType).toBe( "ValidationError" );
    expect(r.validationMessages).toBeDefined();
    expect(r.validationMessages![0].message).toBe("This is an info from the command validation.");
    expect(r.validationMessages![0].depth).toBe( 0 );
    expect(r.validationMessages![0].level).toBe( UserMessageLevel.Info );
    expect(r.validationMessages![1].message).toBe("The BuggyCommand is not valid (by design).");
    expect(r.validationMessages![1].depth).toBe( 1 );
    expect(r.validationMessages![1].level).toBe( UserMessageLevel.Error );
    expect(r.validationMessages![2].message).toBe("This is a warning from the command validation.");
    expect(r.validationMessages![2].depth).toBe( 1 );
    expect(r.validationMessages![2].level).toBe( UserMessageLevel.Warn );
});
it('A command can return a SimpleMessage.', async () => {
  const ep = new HttpCrisEndpoint(axios, crisEndpoint);
  const cmd = CommandWithMessage.create();
  var r = await ep.sendOrThrowAsync(cmd);
  expect(r instanceof SimpleUserMessage );
  expect(r.message.startsWith("Local servert time is"));
  expect(r.depth).toBe( 0 );
  expect(r.level).toBe( UserMessageLevel.Info );
});
