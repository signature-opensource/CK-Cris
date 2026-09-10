An in-memory implementation of delayed command execution: a priority queue, a timer, and a handoff to
the background executor when the date arrives.

Nothing survives a restart, and the class is built to be replaced rather than trusted with durability -
storing, the pre-execution hook and the handler itself are all overridable, and the in-memory queue
stays available to a persistent implementation for its near-term entries.

A past execution date is refused at validation unless explicitly allowed, and a command whose date has
passed still runs through the background context rather than inline. A routed event is raised once it
has run, carrying the command and its result.
