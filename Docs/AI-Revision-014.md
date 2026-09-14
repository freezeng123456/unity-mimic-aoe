# AI revision 0.1.4

The controller now treats production as an affordability decision: it tries the configured batch and degrades to one unit when the actual `World` validator rejects the batch. This is especially relevant to cannon, whose cost and population reservation commonly make a two-unit batch invalid. No resources or units are created outside `World.Issue`.

The AI keeps fog-limited enemy observation and uses the same economy, technology, age and shipment rules as the player. Foundry cannon production is attempted only when the catalog and current resources permit it. Civilization-specific villager and bank behavior remains owned by the catalog/economy layer; this revision does not claim a complete second-civilization strategy or full IV–V balance.

Not verified in this revision: Unity compilation, native client play, successful cannon output in a complete duel, long-term economic stability, full five-age progression, and human control quality.
