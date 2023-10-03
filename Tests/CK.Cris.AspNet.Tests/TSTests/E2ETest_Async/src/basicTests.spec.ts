import axios from "axios"; 
import { HttpCrisEndpoint, AmbientValues } from "@local/ck-gen"; 
import { BeautifulCommand } from "@local/ck-gen"; 
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

