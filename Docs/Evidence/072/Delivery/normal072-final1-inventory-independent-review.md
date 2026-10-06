# Normal072 final1 independent inventory review

Status: passed inventory integrity review only. Consistent / observed_routes_incomplete is not all routes successful.

Files: 126 files, 18 directories; 4247404839 file bytes; lstat namespace/type/mode/size/SHA256 agree and before/after identities stable.

Source:718 frozen inputs; before/after/full-test maps identical; actual runner exit0/sourceStable=true; receipt refs match.

| Case | Requested | Classification | Age | End seconds | Winner | Comparison |
| --- | --- | --- | --- | --- | --- | --- |
| traders_bend070-coast-42 | revolution | ended_before_route | 4 | 2158.855 | 1 | natural_terminal_before_matched_horizon |
| traders_bend070-coast-43 | empire | route_reached | 5 | 2458.748 | 1 | matched_horizon_reached |
| traders_bend070-river-42 | revolution | route_reached | 4 | 3600.061 | -1 | matched_horizon_reached |
| traders_bend070-river-43 | empire | ended_before_route | 4 | 2107.205 | 1 | natural_terminal_before_matched_horizon |
| traders_bend070-france-42 | revolution | route_reached | 4 | 2726.809 | 0 | matched_horizon_reached |
| traders_bend070-france-43 | empire | route_reached | 5 | 2944.922 | 0 | matched_horizon_reached |
| traders_bend070-ottoman-42 | revolution | route_reached | 4 | 2228.523 | 0 | natural_terminal_before_matched_horizon |
| traders_bend070-ottoman-43 | empire | ended_before_route | 4 | 2214.610 | 1 | natural_terminal_before_matched_horizon |
| winding-coast-44 | revolution | route_reached | 4 | 1849.296 | 0 | natural_terminal_before_matched_horizon |
| winding-coast-45 | empire | route_reached | 5 | 2746.128 | 0 | matched_horizon_reached |
| winding-river-44 | revolution | route_reached | 4 | 1847.996 | 0 | natural_terminal_before_matched_horizon |
| winding-river-45 | empire | ended_before_route | 4 | 1693.134 | 1 | natural_terminal_before_matched_horizon |
| winding-france-44 | revolution | route_reached | 4 | 2083.882 | 1 | natural_terminal_before_matched_horizon |
| winding-france-45 | empire | ended_before_route | 4 | 1764.116 | 1 | natural_terminal_before_matched_horizon |
| winding-ottoman-44 | revolution | ended_before_route | 2 | 1982.763 | 1 | natural_terminal_before_matched_horizon |
| winding-ottoman-45 | empire | ended_before_route | 2 | 1725.526 | 1 | natural_terminal_before_matched_horizon |

Counts: {"civilianEconomyRecovered": 6, "civilianSourcesRestored": 6, "earlierNaturalTerminalProjection": 11, "empireReached": 3, "endedBeforeRoute": 7, "executedClosed": 16, "fixedBudgetEndWithRouteReached": 1, "matched2400Projection": 5, "naturalTerminal": 15, "rebuildCompleted": 6, "revolutionReached": 6, "routeBudgetTimeout": 0, "routeReached": 9}

Limits:

- 9/16 requested routes reached. Seven games ended naturally before route completion. No claim that all16 routes passed.
- Revolution reaches6/8, Empire reaches3/8;6 successful revolutions completed government rebuilding and civilian recovery.
- Only5 games reached matched2400s;11 have earlier natural-terminal projections and cannot be claimed equal-horizon balance results.
- 15 natural terminal games and1 predeclared3600s budget ending with its route already reached.
- Balance not assessed: exact scope is two maps, four civilizations and fixed16seeds.
- This stored-tree inventory/source-map consistency review does not repeat checker/economics analyzer or grant whole-release acceptance.

Inventory SHA256: 2e563fa357b58ea015c84d6cad24e233fcacd34f40923ea7bb702927b139b471
Checker SHA256: 8c21268cdcc7bbbaf4db8a7f6c3bca7d1a7e2e59b88eff841e45cfc5b4f762b7
JSON review SHA256: ca339cf5148a0b4889647822b8faf09901c080bac98af94a618b2c398391c42f
