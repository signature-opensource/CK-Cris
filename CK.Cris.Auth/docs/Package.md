The service that gives the Cris authentication parts their meaning.

It validates an incoming command against the current authentication - the actor identifier must match
the last authenticated user, and a command demanding a normal or critical level must find one - and it
does the reverse in a background context, rebuilding an authentication from the values the command
carries. That symmetry is why the values are on the command: checked where a trusted source exists,
authoritative where none does.

Registering it is what turns a declared authentication part into an enforced one. Every method is
virtual, so a rule can be tightened without touching the parts.
