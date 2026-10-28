#!/usr/bin/env python3
"""Read-only 071 source/XML audit. All outputs belong to this Diagnostics folder."""
import argparse
import hashlib
import json
import re
import subprocess
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from xml.etree import ElementTree as ET

OWNED = Path(__file__).resolve().parent
PROJECT = Path('/Users/Apple_501/Documents/Codex/work/frontier-reckoning/project')
EXPECTED_FP = '438caad7a72e131f2065ba846ff27a8a188f82b5d3ca12e0948baa24e15f2a20'
BASELINE_FP = 'ab5ae20e5ff0de9d5e3b2fdd9346ed9f158cb4082a05933e4d6b074cd7333047'
INPUT_DIRS = ('Assets', 'Packages', 'ProjectSettings', 'Tools')
NEW_CLASSES = {
    'EditMode': {'Frontier.Tests.AiRevolution071Tests': 36,
                 'Frontier.Tests.Revolution071Tests': 69,
                 'Frontier.Tests.RevolutionLan071Tests': 1,
                 'Frontier.Tests.RevolutionShipment071Tests': 2,
                 'Frontier.Tests.ReplayCatalogQuality071Tests': 7},
    'PlayMode': {'Frontier.Tests.RevolutionHud071Tests': 5,
                 'Frontier.Tests.TerminalObjectiveHud071Tests': 2}}
EXPECTED_TOTALS = {'EditMode': 1082, 'PlayMode': 404}
BASELINE_TOTALS = {'EditMode': 967, 'PlayMode': 397}


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        while chunk := stream.read(1024 * 1024):
            digest.update(chunk)
    return digest.hexdigest()


def fingerprint(files):
    return hashlib.sha256(json.dumps(files, sort_keys=True, separators=(',', ':')).encode()).hexdigest()


def inventory(project):
    result = {}
    for directory in INPUT_DIRS:
        for path in sorted((project / directory).rglob('*')):
            parts = path.relative_to(project).parts
            if path.is_file() and not any(part.startswith('.') or part == '__pycache__' for part in parts):
                result[path.relative_to(project).as_posix()] = sha(path)
    return result


def own_path(path):
    target = Path(path).resolve()
    target.relative_to(OWNED)
    return target


def save_json(path, value):
    target = own_path(path)
    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(value, ensure_ascii=False, indent=2) + '\n')


def timestamp(value):
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


def read_json(path, errors):
    try:
        return json.loads(Path(path).read_text())
    except (OSError, ValueError) as error:
        errors.append({'path': str(path), 'error': str(error)})
        return {}


def report(path, errors):
    try:
        root = ET.parse(path).getroot()
        leaves = [dict(node.attrib) for node in root.iter('test-case')]
        results = Counter(case.get('result', '<missing>') for case in leaves)
        names = [case.get('fullname', '') for case in leaves]
        duplicates = sorted(name for name, count in Counter(names).items() if count > 1)
        classes = Counter(case.get('classname', '<missing>') for case in leaves)
        return {'available': True, 'path': str(path), 'sha256': sha(path), 'rootTag': root.tag,
                'root': dict(root.attrib), 'leafCount': len(leaves), 'leafResults': dict(results),
                'uniqueFullnames': len(set(names)), 'duplicateFullnames': duplicates,
                'missingFullnames': names.count(''), 'classCounts': dict(sorted(classes.items())),
                'nonPassed': [case for case in leaves if case.get('result') != 'Passed'],
                'fullnames': sorted(names), 'leaves': leaves}
    except (OSError, ValueError, ET.ParseError) as error:
        errors.append({'path': str(path), 'error': str(error)})
        return {'available': False, 'path': str(path), 'leafCount': 0, 'leafResults': {}, 'root': {},
                'fullnames': [], 'leaves': [], 'classCounts': {}}


def all_passed(item):
    return item['available'] and item['leafCount'] > 0 and item['leafResults'] == {'Passed': item['leafCount']}


def root_matches(item, total):
    root = item['root']
    try:
        return item['available'] and item['rootTag'] == 'test-run' and root['result'] == 'Passed' and \
            int(root['total']) == int(root['passed']) == item['leafCount'] == total and \
            int(root.get('testcasecount', total)) == total and \
            all(int(root.get(key, 0)) == 0 for key in ('failed', 'skipped', 'inconclusive'))
    except (ValueError, KeyError):
        return False


def unique(item):
    return item['available'] and not item['duplicateFullnames'] and item['missingFullnames'] == 0


def json_binding(document, expected):
    files = document.get('files')
    return document.get('schema') == 1 and isinstance(files, dict) and bool(files) and \
        document.get('sourceFingerprint') == fingerprint(files) == expected


def audit(args, files):
    errors = []
    new_classes, expected_totals = args.new_classes, args.expected_totals
    current_fp = fingerprint(files)
    freeze = read_json(args.freeze, errors)
    evidence_path = args.run / 'source-evidence.json'
    evidence = read_json(evidence_path, errors)
    baseline_evidence_path = args.baseline / 'source-evidence.json'
    baseline_evidence = read_json(baseline_evidence_path, errors)
    reports = {mode: report(args.run / (mode + '.xml'), errors) for mode in expected_totals}
    baseline = {mode: report(args.baseline / (mode + '.xml'), errors) for mode in expected_totals}
    checks = {
        'current_input_count_matches_declared_count': len(files) == args.input_count,
        'current_fingerprint_matches_declared_freeze': current_fp == args.fingerprint,
        'independent_freeze_fingerprint_is_valid_and_expected': json_binding(freeze, args.fingerprint),
        'current_files_equal_independent_frozen_files': files == freeze.get('files'),
        'test_evidence_fingerprint_is_valid_and_expected': json_binding(evidence, args.fingerprint),
        'current_files_equal_tested_files': files == evidence.get('files'),
        'application_source_version_is_0_14_0': bool(re.search(r'^\s*bundleVersion:\s*0\.14\.0\s*$',
            (args.project / 'ProjectSettings/ProjectSettings.asset').read_text(), re.MULTILINE)),
        'input_scope_contains_only_expected_relative_paths_and_sha256': bool(files) and all(
            Path(path).parts[0] in INPUT_DIRS and not Path(path).is_absolute() and '..' not in Path(path).parts
            and re.fullmatch('[0-9a-f]{64}', digest) for path, digest in files.items()),
    }
    preservation = {}
    for mode, item in reports.items():
        recorded = evidence.get('reports', {}).get(mode, {})
        checks[mode + '_root_totals_equal_actual_leaves_and_expected_count'] = root_matches(item, expected_totals[mode])
        checks[mode + '_actual_leaves_all_passed_zero_skip'] = all_passed(item)
        checks[mode + '_actual_fullnames_are_present_and_unique'] = unique(item)
        checks[mode + '_xml_sha_and_passed_total_match_evidence'] = item['available'] and \
            recorded.get('file') == mode + '.xml' and recorded.get('sha256') == item.get('sha256') and \
            recorded.get('passed') == item['leafCount']
        passed_names = {case.get('fullname') for case in item['leaves'] if case.get('result') == 'Passed'}
        old_names = set(baseline[mode]['fullnames'])
        missing = sorted(old_names - passed_names)
        added = sorted(set(item['fullnames']) - old_names)
        checks[mode + '_all_070_baseline_cases_preserved_and_passed'] = \
            len(old_names) == BASELINE_TOTALS[mode] and not missing
        preservation[mode] = {'baselineCount': len(old_names), 'preservedPassedCount': len(old_names & passed_names),
                              'missingOrNotPassed': missing, 'addedCount': len(added), 'addedFullnames': added}
    checks['baseline_070_evidence_fingerprint_is_valid_and_original'] = json_binding(baseline_evidence, BASELINE_FP)
    checks['baseline_070_xml_sha_matches_original_source_evidence'] = all(
        baseline[mode]['available'] and baseline_evidence.get('reports', {}).get(mode, {}).get('sha256') == baseline[mode].get('sha256')
        for mode in expected_totals)
    checks['baseline_070_actual_counts_967_397_all_passed_and_unique'] = all(
        all_passed(baseline[mode]) and unique(baseline[mode]) and root_matches(baseline[mode], BASELINE_TOTALS[mode])
        for mode in expected_totals)
    for mode, expected_classes in new_classes.items():
        checks[mode + '_071_exact_class_leaf_counts_and_passed_results'] = all(
            reports[mode]['classCounts'].get(name) == count and
            all(case.get('result') == 'Passed' for case in reports[mode]['leaves'] if case.get('classname') == name)
            for name, count in expected_classes.items())
    checks['all_new_leaf_fullnames_are_exactly_the_declared_071_classes'] = all(
        set(preservation[mode]['addedFullnames']) ==
        {case.get('fullname') for case in reports[mode]['leaves'] if case.get('classname') in new_classes[mode]}
        and preservation[mode]['addedCount'] == sum(new_classes[mode].values()) for mode in expected_totals)
    try:
        checks['actual_xml_order_and_source_evidence_record_time_are_consistent'] = \
            timestamp(reports['EditMode']['root']['start-time']) <= timestamp(reports['EditMode']['root']['end-time']) <= \
            timestamp(reports['PlayMode']['root']['start-time']) <= timestamp(reports['PlayMode']['root']['end-time']) <= \
            timestamp(evidence['recordedAtUtc'])
    except (KeyError, ValueError):
        checks['actual_xml_order_and_source_evidence_record_time_are_consistent'] = False
    git = subprocess.run(['git', 'rev-parse', 'HEAD'], cwd=args.project, capture_output=True, text=True)
    build = {'status': 'pending', 'verifiedByThisAudit': False}
    if args.build_identity:
        receipt = read_json(args.build_identity, errors)
        build = {'status': 'receipt_supplied_for_reference_only', 'verifiedByThisAudit': False,
                 'path': str(args.build_identity), 'sha256': sha(args.build_identity) if args.build_identity.is_file() else None,
                 'receipt': receipt}
    result = {'schema': 1, 'release': '071', 'version': '0.14.0', 'candidate': 1,
              'auditedAtUtc': datetime.now(timezone.utc).isoformat(), 'auditor': 'content070_audit',
              'scope': 'Independent runtime input SHA and full NUnit XML leaf audit. No Unity launch, source/docs edits, native input or binary build validation.',
              'allChecksPassed': all(checks.values()) and not errors, 'checkCount': len(checks), 'checks': checks,
              'errors': errors, 'runtimeCommitObserved': git.stdout.strip() if git.returncode == 0 else None,
              'sourceFingerprint': current_fp, 'sourceInputs': len(files), 'expectedSourceFingerprint': args.fingerprint,
              'testRun': str(args.run), 'baseline070': str(args.baseline),
              'passedTotal': sum(item['leafResults'].get('Passed', 0) for item in reports.values()),
              'expectedTotals': expected_totals, 'actualTotals': {mode: item['leafCount'] for mode, item in reports.items()},
              'sourceBinding': {'independentFreezePath': str(args.freeze), 'testEvidencePath': str(evidence_path),
                                'testEvidenceSha256': sha(evidence_path) if evidence_path.is_file() else None,
                                'testEvidenceRecordedAtUtc': evidence.get('recordedAtUtc'),
                                'filesDifferingFromFrozen': sorted(path for path in set(files) | set(freeze.get('files', {}))
                                    if files.get(path) != freeze.get('files', {}).get(path)),
                                'filesDifferingFromTested': sorted(path for path in set(files) | set(evidence.get('files', {}))
                                    if files.get(path) != evidence.get('files', {}).get(path))},
              'baselinePreservation': preservation, 'new071ClassesExpected': new_classes,
              'reports': {mode: {key: value for key, value in item.items() if key != 'leaves'} for mode, item in reports.items()},
              'build': build, 'native': {'status': 'pending', 'verifiedByThisAudit': False}}
    save_json(args.output_json, result)
    lines = ['# 071 独立源码与全回归审计', '',
             '结论：' + ('全部检查通过。' if result['allChecksPassed'] else '未通过；失败或缺失项目如下。'), '',
             f"源码输入 {len(files)} 个；当前 SHA 指纹 `{current_fp}`。",
             f"审计 {len(checks)} 项；测试目录 `{args.run}`。", '',
             '| 模式 | 实际叶用例 | Passed | 070 旧例保留 | 新增 |', '|---|---:|---:|---:|---:|']
    for mode in expected_totals:
        item, kept = reports[mode], preservation[mode]
        lines.append(f"| {mode} | {item['leafCount']} | {item['leafResults'].get('Passed', 0)} | {kept['preservedPassedCount']}/{BASELINE_TOTALS[mode]} | {kept['addedCount']} |")
    lines.extend(['', '071 新类实际叶用例：'])
    for mode, expected_classes in new_classes.items():
        for name, count in expected_classes.items():
            lines.append(f"- {mode} {name}: {reports[mode]['classCounts'].get(name, 0)}（预期 {count}）")
    failed = [name for name, passed in checks.items() if not passed]
    if failed or errors:
        lines.extend(['', '失败或缺失：'] + ['- ' + name for name in failed] +
                     ['- ' + error['path'] + ': ' + error['error'] for error in errors])
    lines.extend(['', '检查实际 test-case 叶结果与根统计、零跳过、完整 fullname 去重；独立重新计算当前文件及冻结/测试清单指纹，并核 XML 与 source-evidence SHA。',
                  '对照原 070 完整 XML 的每个旧 fullname；旧用例通过不表示本次商业游戏完整度、自然经营平衡或原生窗口已经验收。',
                  '构建与原生状态均未由此脚本验证。提供 build-identity 只保存参考回执，不自动变成构建通过结论。', ''])
    own_path(args.output_md).write_text('\n'.join(lines))
    print(json.dumps({'allChecksPassed': result['allChecksPassed'], 'checkCount': len(checks),
                      'actualTotals': result['actualTotals'], 'passedTotal': result['passedTotal'],
                      'sourceFingerprint': current_fp, 'failedChecks': failed, 'errors': errors,
                      'json': str(args.output_json), 'markdown': str(args.output_md)}, ensure_ascii=False))
    return 0 if result['allChecksPassed'] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--project', type=Path, default=PROJECT)
    parser.add_argument('--run', type=Path, default=PROJECT / 'Builds/Tests/run-uYnxt328')
    parser.add_argument('--baseline', type=Path, default=PROJECT / 'Builds/Tests/run-u4J3ZXEB')
    parser.add_argument('--fingerprint', default=EXPECTED_FP)
    parser.add_argument('--input-count', type=int, default=702)
    parser.add_argument('--new-class', action='append', default=[], metavar='MODE:FULL_CLASS_NAME:COUNT',
                        help='Explicit new-class leaf expectation; may add a class or override its declared count. Repeat for multiple classes.')
    parser.add_argument('--freeze', type=Path, default=OWNED / 'independent071-source-freeze.json')
    parser.add_argument('--output-json', type=Path, default=OWNED / 'independent071-test-audit.json')
    parser.add_argument('--output-md', type=Path, default=OWNED / 'independent071-test-audit.md')
    parser.add_argument('--build-identity', type=Path)
    parser.add_argument('--capture-freeze', action='store_true', help='Capture current independent hashes; do not audit incomplete tests.')
    parser.add_argument('--capture-timing', default='Independent read-only source capture; no test completion implied.',
                        help='Truthful timing/provenance for this independent source capture.')
    args = parser.parse_args()
    if not re.fullmatch('[0-9a-f]{64}', args.fingerprint) or args.input_count <= 0:
        parser.error('Fingerprint must be SHA-256 and input-count must be positive')
    args.new_classes = {mode: dict(classes) for mode, classes in NEW_CLASSES.items()}
    supplied_classes = set()
    for value in args.new_class:
        parts = value.split(':')
        if len(parts) != 3 or parts[0] not in BASELINE_TOTALS or not re.fullmatch(r'[A-Za-z_]\w*(?:\.[A-Za-z_]\w*)+', parts[1]) or not parts[2].isdigit() or int(parts[2]) <= 0:
            parser.error('new-class must be EditMode/PlayMode:Full.Class.Name:POSITIVE_COUNT')
        mode, name, count = parts
        if (mode, name) in supplied_classes:
            parser.error('Duplicate explicit new-class expectation: ' + mode + ':' + name)
        supplied_classes.add((mode, name))
        args.new_classes[mode][name] = int(count)
    args.expected_totals = {mode: BASELINE_TOTALS[mode] + sum(classes.values()) for mode, classes in args.new_classes.items()}
    for name in ('freeze', 'output_json', 'output_md'):
        setattr(args, name, own_path(getattr(args, name)))
    files = inventory(args.project)
    if args.capture_freeze:
        current_fp = fingerprint(files)
        if current_fp != args.fingerprint or len(files) != args.input_count:
            parser.exit(1, 'Current inputs do not match the declared freeze; no freeze file was written.\n')
        if args.freeze.exists():
            existing = json.loads(args.freeze.read_text())
            if existing.get('files') != files:
                parser.exit(1, 'Independent freeze already exists for different inputs; preserving it.\n')
        else:
            save_json(args.freeze, {'schema': 1, 'version': '0.14.0', 'release': '071', 'candidate': 1,
                                   'capturedAtUtc': datetime.now(timezone.utc).isoformat(),
                                   'captureTiming': args.capture_timing,
                                   'sourceFingerprint': current_fp, 'sourceInputs': len(files), 'files': files})
        print(json.dumps({'sourceFingerprint': current_fp, 'sourceInputs': len(files), 'freeze': str(args.freeze)}))
        return 0
    return audit(args, files)


if __name__ == '__main__':
    raise SystemExit(main())
