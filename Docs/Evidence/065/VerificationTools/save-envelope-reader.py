#!/usr/bin/env python3
"""Read-only bounded SaveEnvelope 1/2 decoding; no game restoration verdict.

Schema 1 stores exact UTF-8 SavedMatch JSON in payload. Schema 2 stores one
canonical Base64 gzip stream in payloadBase64 with encoding=gzip-base64.
Both hash the uncompressed UTF-8 bytes. File limit is 64 MiB; raw limit is
256 MiB, inclusive. Gzip CRC/EOF and SHA are checked independently.
"""
import base64
import binascii
import hashlib
import json
import math
from pathlib import Path
import re
import zlib

MAX_FILE_BYTES = 64 * 1024 * 1024
MAX_PAYLOAD_BYTES = 256 * 1024 * 1024
BASE64_CHUNK_CHARS = 64 * 1024
OUTPUT_CHUNK_BYTES = 64 * 1024
HEX64 = re.compile(r'[0-9a-f]{64}\Z')


def require(condition, message):
    if not condition:
        raise ValueError(message)


def strict_json(text):
    def pairs(items):
        result = {}
        for key, value in items:
            require(key not in result, 'Duplicate JSON key: ' + key)
            result[key] = value
        return result

    def invalid(value):
        raise ValueError('Non-finite JSON value: ' + value)

    def finite_float(value):
        result = float(value)
        require(math.isfinite(result), 'Non-finite JSON number: ' + value)
        return result

    return json.loads(text, object_pairs_hook=pairs, parse_constant=invalid,
                      parse_float=finite_float)


def compressed_payload(encoded):
    require(isinstance(encoded, str) and bool(encoded), 'Missing gzip-base64 payload')
    require(len(encoded) % 4 == 0, 'Base64 length is not a multiple of four')
    decoder = zlib.decompressobj(16 + zlib.MAX_WBITS)
    output = bytearray()
    digest = hashlib.sha256()
    compressed_bytes = 0
    for start in range(0, len(encoded), BASE64_CHUNK_CHARS):
        segment = encoded[start:start + BASE64_CHUNK_CHARS]
        try:
            chunk = base64.b64decode(segment, validate=True)
        except (ValueError, binascii.Error) as error:
            raise ValueError('Invalid canonical Base64 payload') from error
        require(base64.b64encode(chunk).decode('ascii') == segment,
                'Non-canonical Base64 padding or unused bits')
        require(start + len(segment) == len(encoded) or '=' not in segment,
                'Base64 padding before the end')
        if start == 0:
            require(len(chunk) >= 18 and chunk[:4] == b'\x1f\x8b\x08\x00',
                    'Expected a complete gzip header with no optional flags')
        require(not decoder.eof, 'Data after the single gzip stream')
        compressed_bytes += len(chunk)
        pending = chunk
        while pending:
            try:
                piece = decoder.decompress(
                    pending, min(OUTPUT_CHUNK_BYTES, MAX_PAYLOAD_BYTES - len(output) + 1))
            except zlib.error as error:
                raise ValueError('Invalid gzip stream or checksum') from error
            require(len(output) + len(piece) <= MAX_PAYLOAD_BYTES,
                    'Uncompressed save exceeds the 256 MiB limit')
            output.extend(piece)
            digest.update(piece)
            pending = decoder.unconsumed_tail
            require(not decoder.unused_data, 'Trailing bytes or concatenated gzip stream')
            if decoder.eof:
                require(not pending, 'Trailing gzip data')
                break
    require(decoder.eof, 'Truncated gzip stream; complete footer required')
    require(bool(output), 'Empty SavedMatch payload')
    try:
        text = output.decode('utf-8', errors='strict')
    except UnicodeDecodeError as error:
        raise ValueError('SavedMatch payload is not strict UTF-8') from error
    return text, len(output), digest.hexdigest(), compressed_bytes


def decode_envelope(raw):
    require(isinstance(raw, bytes), 'Envelope must be raw bytes')
    require(0 < len(raw) <= MAX_FILE_BYTES, 'Save file exceeds the 64 MiB limit or is empty')
    try:
        envelope = strict_json(raw.decode('utf-8', errors='strict'))
    except UnicodeDecodeError as error:
        raise ValueError('Save envelope is not strict UTF-8') from error
    require(isinstance(envelope, dict) and type(envelope.get('schema')) is int,
            'Invalid SaveEnvelope schema')
    schema = envelope['schema']
    expected = envelope.get('sha256')
    require(isinstance(expected, str) and HEX64.fullmatch(expected),
            'SavedMatch checksum must be a lowercase SHA-256')
    compressed_bytes = None
    if schema == 1:
        require(envelope.get('encoding') in (None, '')
                and envelope.get('payloadBase64') in (None, ''),
                'Ambiguous legacy encoded payload')
        text = envelope.get('payload')
        require(isinstance(text, str) and bool(text), 'Missing legacy SavedMatch payload')
        try:
            payload = text.encode('utf-8', errors='strict')
        except UnicodeEncodeError as error:
            raise ValueError('Legacy payload is not strict UTF-8') from error
        require(len(payload) <= MAX_PAYLOAD_BYTES, 'Uncompressed save exceeds the 256 MiB limit')
        payload_bytes, actual = len(payload), hashlib.sha256(payload).hexdigest()
        encoding = 'plain-json'
    elif schema == 2:
        require(envelope.get('encoding') == 'gzip-base64', 'Unsupported compressed save encoding')
        require(envelope.get('payload') in (None, ''), 'Ambiguous schema-2 legacy payload')
        text, payload_bytes, actual, compressed_bytes = compressed_payload(envelope.get('payloadBase64'))
        encoding = 'gzip-base64'
    else:
        raise ValueError('Unsupported SaveEnvelope schema: ' + str(schema))
    require(actual == expected, 'SavedMatch uncompressed payload checksum mismatch')
    saved = strict_json(text)
    require(isinstance(saved, dict), 'SavedMatch JSON must be an object')
    return {'raw': raw, 'saved': saved, 'payloadText': text, 'payloadSha256': actual,
            'payloadBytes': payload_bytes, 'schema': schema, 'encoding': encoding,
            'compressedBytes': compressed_bytes}


def read_envelope(path):
    path = Path(path)
    require(path.is_file() and not path.is_symlink(),
            'Expected a regular non-symlink save file: ' + str(path))
    require(0 < path.stat().st_size <= MAX_FILE_BYTES, 'Save file exceeds the 64 MiB limit or is empty')
    with path.open('rb') as stream:
        raw = stream.read(MAX_FILE_BYTES + 1)
    return decode_envelope(raw)
