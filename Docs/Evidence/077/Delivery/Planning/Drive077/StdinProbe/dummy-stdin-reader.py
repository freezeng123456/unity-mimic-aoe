#!/usr/bin/env python3
# Harmless transport probe only, not a Drive helper or cloud action.
import hashlib,json,sys
mode=sys.argv[1]
if mode=='compact':
 raw=sys.stdin.readline(1024*1024)
else:
 lines=[]
 while True:
  line=sys.stdin.readline(1024*1024)
  if line=='PROBE_END_077\n': break
  if not line: raise ValueError('EOF before probe terminator')
  lines.append(line)
 raw=''.join(lines)
payload=json.loads(raw)
expected={'kind':'dummy-077-stdin','multiline':'row one\nrow two\nrow three','long':'X'*12000,'count':12000}
assert payload==expected and len(raw.encode())>8192
print(json.dumps({'mode':mode,'bytes':len(raw.encode()),'sha256':hashlib.sha256(raw.encode()).hexdigest(),'decodedMultiline':payload['multiline'].count('\n')==2,'longCharacters':len(payload['long']),'roundTripExact':payload==expected}))
