/* FUTURE ONLY. Function expression: loading/evaluating this file performs no I/O.
   Invoke phases deliberately after root closes actual 077 packaging/native gates.
   No credentials, signed fetch references, preview text or base64 enter journals. */
(function makeDrive077(tools, config) {
  const PARENT = '1Dgm7Nx9Qwdaz-vhClbRekJ134tEOW4GH';
  const C1 = '1CgcNrTzLaVKXoDwtBTaYJ1I2mD7Wegnv';
  const HELPER = '/Users/Apple_501/Documents/Codex/outputs/frontier-reckoning-0.20.0/Candidate1/Planning/Drive077/drive077-local.py';
  const need = (ok, message) => { if (!ok) throw Error(message); };
  const quote = s => "'" + String(s).replaceAll("'", "'\\''") + "'";
  let serial = 0;
  function unwrap(r) {
    need(r && !r.isError && r.structuredContent, 'Connector returned no structured success');
    return r.structuredContent;
  }
  function owner(m) {
    const p = m.permissions;
    need(m.shared === false && m.source_visibility_status === 'permission_metadata_available' && Array.isArray(p) && p.length > 0 && p.every(x => x.type === 'user' && x.role === 'owner'), 'Fresh owner-only permissions required');
    const ids = p.map(x => x.emailAddress || x.id);
    need(ids.every(Boolean) && new Set(ids).size === ids.length, 'Owner identity missing/duplicated');
    return ids.sort().join('\n');
  }
  async function local(args, input) {
    const cmd = 'python3 ' + quote(HELPER) + ' ' + args.map(quote).join(' ');
    let r = await tools.exec_command({cmd, max_output_tokens: 12000, yield_time_ms: 1000});
    let output = r.output || '';
    if (input !== undefined) {
      need(r.session_id, 'Expected local verifier awaiting one stdin JSON line');
      r = await tools.write_stdin({session_id:r.session_id,chars:JSON.stringify(input)+'\n',max_output_tokens:12000,yield_time_ms:1000});
      output += r.output || '';
    }
    while (r.session_id) {
      r = await tools.write_stdin({session_id:r.session_id,chars:'',max_output_tokens:12000,yield_time_ms:1000});
      output += r.output || '';
    }
    need(r.exit_code === 0, 'Local verification failed; inspect retained local artifacts, not raw references');
    return JSON.parse(output.trim());
  }
  async function journal(label, data) {
    const n = String(Date.now()) + '-' + (++serial) + '-' + label + '.json';
    return local(['journal','--directory',config.ledgerDir,'--name',n],data);
  }
  async function plan() {
    const p = await local(['verify-plan','--plan',config.planPath]);
    need(p.state === 'LOCAL_PLAN_NOT_UPLOADED' && p.parentId === PARENT && p.protectedC1Id === C1 && p.logicalZipCount === 3 && p.releaseIdentity.version === '0.20.0', 'Wrong immutable upload plan');
    return p;
  }
  async function metadata(id) {
    const m = unwrap(await tools.mcp__codex_apps__google_drive_get_file_metadata({fileId:id}));
    need(m.id === id, 'Returned metadata ID differs'); return m;
  }
  async function children(id) {
    const all = [], tokens = new Set(); let token;
    do {
      const args = {special_filter_query_str:"'"+id+"' in parents and trashed = false",topn:100,best_effort_fetch:false};
      if (token) args.page_token = token;
      const page = unwrap(await tools.mcp__codex_apps__google_drive_search(args));
      need(Array.isArray(page.results), 'Search rows missing');
      need(page.results.every(x => x.id && Array.isArray(x.parent_ids) && x.parent_ids.length === 1 && x.parent_ids[0] === id), 'Search scope differs');
      all.push(...page.results); token = page.next_page_token || null;
      need(token || page.results.length < 100, 'Complete listing ambiguous at page ceiling');
      if (token) { need(!tokens.has(token), 'Repeated search cursor'); tokens.add(token); }
    } while (token);
    need(new Set(all.map(x=>x.id)).size === all.length, 'Duplicate search IDs'); return all;
  }
  async function candidate(p, state) {
    need(state.folder && state.folder.id !== PARENT && state.folder.id !== C1, 'Protected folder is not the candidate');
    const m = await metadata(state.folder.id);
    need(m.title === p.folderName && m.mime_type === 'application/vnd.google-apps.folder' && JSON.stringify(m.parent_ids) === JSON.stringify([PARENT]) && owner(m) === owner(state.parent), 'Candidate folder identity/owner differs'); return m;
  }
  async function preflight(p) {
    const parent = await metadata(PARENT); owner(parent);
    need(parent.mime_type === 'application/vnd.google-apps.folder', 'Project parent type differs');
    const projectBefore = await children(PARENT);
    need(projectBefore.some(x=>x.id===C1) && !projectBefore.some(x=>x.title===p.folderName || /^0\.20\.0(?:\s|$)/.test(x.title)), 'Candidate already exists or C1 missing; reconcile without creating a duplicate');
    const c1 = await metadata(C1);
    need(owner(c1) === owner(parent) && JSON.stringify(c1.parent_ids) === JSON.stringify([PARENT]), 'Protected C1 owner/parent differs');
    const state = {parent,projectBefore,c1Before:await children(C1),uploads:[],readbacks:{}};
    await journal('preflight',state); return state;
  }
  async function createCandidate(p, state, allow) {
    need(allow?.cloudWrites === true && !state.folder, 'Explicit future cloud-write gate required');
    await plan();
    const latest = await children(PARENT);
    need(JSON.stringify(latest.map(x=>x.id).sort()) === JSON.stringify(state.projectBefore.map(x=>x.id).sort()), 'Parent changed after preflight; reconcile first');
    await journal('create-intent',{name:p.folderName,parent_folder:PARENT});
    let result;
    try { result = unwrap(await tools.mcp__codex_apps__google_drive_create_folder({name:p.folderName,parent_folder:PARENT})); }
    catch (_) { throw Error('Creation outcome uncertain; inspect intent and fresh parent listing before any retry'); }
    need(result.id && result.id !== PARENT && result.id !== C1, 'New folder ID missing');
    await journal('created-id',{id:result.id,title:result.title || result.name,url:result.url || null});
    state.folder = {id:result.id}; state.folder = await candidate(p,state);
    need((await children(state.folder.id)).length === 0, 'New candidate is not empty');
    await journal('created-verified',state.folder); return state;
  }
  async function uploadOne(p, state, fileName, allow) {
    need(allow?.cloudWrites === true, 'Explicit future cloud-write gate required'); await plan();
    const folder = await candidate(p,state), item = p.files.find(x=>x.name===fileName);
    need(item && !state.uploads.some(x=>x.name===fileName), 'Plan file missing/already recorded');
    need(!(await children(folder.id)).some(x=>x.title===fileName), 'Remote name already exists; reconcile by ID and whole bytes, do not upload twice');
    const args = {file_uri:item.path,file_name:item.name,mime_type:item.mime,parent_folder_id:folder.id};
    await journal('upload-intent',{name:item.name,bytes:item.bytes,sha256:item.sha256,request:args});
    let result;
    try { result = unwrap(await tools.mcp__codex_apps__google_drive_upload_file(args)); }
    catch (_) { throw Error('Upload outcome uncertain; inspect intent and fresh candidate listing before any retry'); }
    need(result.id && result.success !== false, 'Uploaded ID missing');
    await journal('uploaded-id',{name:item.name,id:result.id,url:result.url || null});
    const m = await metadata(result.id);
    need(m.title===item.name && Number(m.size)===item.bytes && m.mime_type===item.mime && JSON.stringify(m.parent_ids)===JSON.stringify([folder.id]) && owner(m)===owner(folder), 'Uploaded file metadata differs');
    state.uploads.push({name:item.name,id:m.id}); await journal('uploaded-verified',m); return state;
  }
  async function readBackOne(p, state, fileName, directory) {
    await plan(); const folder = await candidate(p,state), item = p.files.find(x=>x.name===fileName), uploaded = state.uploads.find(x=>x.name===fileName);
    need(item && uploaded && !state.readbacks[fileName], 'Known uploaded ID required; readback already recorded');
    const m = await metadata(uploaded.id); owner(m);
    need(m.url && m.title===item.name && Number(m.size)===item.bytes && m.mime_type===item.mime && JSON.stringify(m.parent_ids)===JSON.stringify([folder.id]) && owner(m)===owner(folder), 'Fresh binary metadata differs');
    const raw = unwrap(await tools.mcp__codex_apps__google_drive_fetch({url:m.url,download_raw_file:true,include_base64:false}));
    // Top-level authenticated output only: never inspect preview content or synthesize a download URL.
    const reference = {};
    if (raw.workspace_path) reference.workspace_path = raw.workspace_path;
    if (raw.file_uri) reference.file_uri = raw.file_uri;
    need(reference.workspace_path || reference.file_uri, 'Raw authenticated file reference missing');
    // Secret reference is used solely as stdin for the future local readback, not text(), logs or journals.
    const r = await local(['readback','--plan',config.planPath,'--name',item.name,'--directory',directory],{fileId:m.id,folderId:folder.id,metadata:m,reference});
    state.readbacks[fileName] = r.receiptPath;
    await journal('readback-complete',{name:r.name,id:r.id,bytes:r.bytes,sha256:r.sha256,receiptPath:r.receiptPath}); return state;
  }
  async function finish(p, state, finalDirectory) {
    await plan(); const parent = await metadata(PARENT), folder = await candidate(p,state), filesMetadata = {};
    need(state.uploads.length===p.files.length && Object.keys(state.readbacks).length===p.files.length, 'Every planned file needs whole-byte readback');
    for (const row of state.uploads) filesMetadata[row.id] = await metadata(row.id);
    const proof = {parent,folder,filesMetadata,readbacks:state.readbacks,folderChildren:await children(folder.id),c1Before:state.c1Before,c1After:await children(C1),projectBefore:state.projectBefore,projectAfter:await children(PARENT)};
    await journal('final-live-metadata',proof);
    return local(['finish','--plan',config.planPath,'--directory',finalDirectory],proof);
  }
  return {plan,metadata,children,preflight,createCandidate,uploadOne,readBackOne,finish};
})
