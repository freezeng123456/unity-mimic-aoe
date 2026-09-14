#!/usr/bin/env python3
"""Compare real simulation event traces. A match is evidence for the supplied case only."""
import argparse,json,hashlib,sys
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('expected',type=Path);p.add_argument('actual',type=Path);args=p.parse_args()
a=args.expected.read_bytes();b=args.actual.read_bytes()
if a==b:
 print(f'PASS: {len(a.splitlines())} events byte-identical; SHA256 {hashlib.sha256(a).hexdigest()}');sys.exit(0)
x=a.splitlines();y=b.splitlines()
for i in range(max(len(x),len(y))):
 if i>=len(x) or i>=len(y) or x[i]!=y[i]:
  print(f'FAIL: first mismatch at event {i}; total {len(x)} vs {len(y)}')
  print('expected:',json.loads(x[i]) if i<len(x) else '<end>');print('actual:',json.loads(y[i]) if i<len(y) else '<end>');sys.exit(1)
print('FAIL: line contents match, but encoding or newline bytes differ');sys.exit(1)
