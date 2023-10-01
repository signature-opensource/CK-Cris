import axios from "axios"; 
import { HttpCrisEndpoint } from "@local/ck-gen"; 
import { BeautifulCommand } from "@local/ck-gen"; 

const crisEndpoint = process.env.CRIS_ENDPOINT_URL ?? "";
const withEndpoint = crisEndpoint ? it : it.skip;

it('isConnect is false until the first command is sent', async () => {
  const ep = new HttpCrisEndpoint( axios, crisEndpoint );
  expect( ep.isConnected ).toBeFalsy();
  await ep.sendAsync( BeautifulCommand.create( "Gorgeous" ) );
  expect( ep.isConnected ).toBeTruthy();
});
