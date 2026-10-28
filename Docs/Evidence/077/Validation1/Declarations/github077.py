"""Existing private draft PR1 only: read-only preflight, optional reviewed body PATCH, readback."""
import argparse,hashlib,json,re,subprocess,urllib.request
from pathlib import Path
from datetime import datetime,timezone
p=argparse.ArgumentParser();p.add_argument('mode',choices=['preflight','update','readback']);p.add_argument('--head',required=True);p.add_argument('--output',type=Path,required=True);p.add_argument('--body',type=Path);p.add_argument('--title');p.add_argument('--prior-body-sha');a=p.parse_args()
assert re.fullmatch('[0-9a-f]{40}',a.head) and not a.output.exists()
c=subprocess.run(['git','credential','fill'],input='protocol=https\nhost=github.com\n\n',text=True,stdout=subprocess.PIPE,stderr=subprocess.PIPE,check=True)
v=dict(x.split('=',1) for x in c.stdout.splitlines() if '=' in x);token=v.get('password');assert token
opener=urllib.request.build_opener(urllib.request.ProxyHandler({}));base='https://api.github.com/repos/freezeng123456/unity-mimic-aoe'
def api(s='',data=None):
 r=urllib.request.Request(base+s,data=None if data is None else json.dumps(data).encode(),headers={'Authorization':'Bearer '+token,'Accept':'application/vnd.github+json','Content-Type':'application/json','User-Agent':'Frontier077-private-delivery'},method='GET' if data is None else 'PATCH')
 with opener.open(r,timeout=90) as f:return json.load(f)
def snapshot():
 repo=api();pr=api('/pulls/1');main=api('/git/ref/heads/main');head=api('/git/ref/heads/codex/land-rts-completion')
 assert repo['private'] is True and repo['visibility']=='private' and pr['state']=='open' and pr['draft'] is True
 assert pr['head']['ref']=='codex/land-rts-completion' and pr['base']['ref']=='main' and pr['number']==1
 assert pr['head']['sha']==head['object']['sha']==a.head and main['object']['sha']=='0bb044adbe40d0cab952e8c3b44e40eca8a6d3fe'
 return dict(repositoryPrivate=True,visibility=repo['visibility'],prUrl=pr['html_url'],state=pr['state'],draft=pr['draft'],headBranch=pr['head']['ref'],headSha=a.head,baseBranch=pr['base']['ref'],mainSha=main['object']['sha'],title=pr['title'],body=pr['body'],bodySha256=hashlib.sha256(pr['body'].encode()).hexdigest())
before=snapshot()
if a.mode in ('update','readback'):
 assert a.body and a.title;body=a.body.read_text()
 if a.mode=='update':
  assert a.prior_body_sha==before['bodySha256'],'Preserve concurrent PR body changes'
  assert before['body'] in body,'Preserve complete prior historical body'
  api('/pulls/1',{'title':a.title,'body':body})
 after=snapshot();assert after['body']==body and after['title']==a.title
else:after=snapshot();assert after==before
receipt=dict(schema=1,mode=a.mode,status='private_open_draft_read_back',recordedUtc=datetime.now(timezone.utc).isoformat(),before=before,after=after,mainUnchanged=before['mainSha']==after['mainSha'],scope='Existing private draft PR1 only. No merge, main write, visibility, release, deployment, or permission change.')
with a.output.open('x') as f:json.dump(receipt,f,indent=2);f.write('\n')
print(json.dumps({k:after[k] for k in ('prUrl','state','draft','headSha','mainSha','bodySha256')}))
