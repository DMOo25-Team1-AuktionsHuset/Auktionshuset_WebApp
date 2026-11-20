import test from 'node:test';
import assert from 'node:assert/strict';
import { status, provision, repair } from '../Auktionshuset/wwwroot/js/usb-station.js';

const local = 'http://127.0.0.1:8765';

test('station status distinguishes disconnected and ready devices', async () => {
    const original = globalThis.fetch;
    try {
        globalThis.fetch = async () => ({ ok: true, json: async () =>
            ({ protocolVersion: 1, stationId: 'station', state: 'unconfigured', connectionId: 'connection' }) });
        assert.equal((await status(local)).state, 'unconfigured');
        globalThis.fetch = async () => ({ ok: true, json: async () =>
            ({ protocolVersion: 1, stationId: 'station', state: 'configured',
                connectionId: 'connection', deviceId: 'device', deviceNumber: 42, verification: 'tls' }) });
        assert.equal((await status(local)).verification, 'tls');
        globalThis.fetch = async () => ({ ok: true, json: async () =>
            ({ protocolVersion: 1, stationId: 'station', state: 'configured',
                connectionId: 'connection', deviceId: 'device', deviceNumber: 42,
                verification: 'ready', verificationGrant: 'signed-proof' }) });
        assert.equal((await status(local)).verificationGrant, 'signed-proof');
        globalThis.fetch = async () => { throw new Error('offline'); };
        assert.equal((await status(local)).state, 'unavailable');
    } finally { globalThis.fetch = original; }
});

test('provisioning sends one local request and returns only safe identity', async () => {
    const original = globalThis.fetch;
    const registration = { deviceId: 'device', deviceNumber: 42, secret: 'S'.repeat(64), grant: 'grant' };
    let calls = 0;
    try {
        globalThis.fetch = async (url, options) => {
            calls++;
            assert.equal(url, `${local}/v1/provision`);
            assert.equal(options.headers['X-Station-Grant'], 'grant');
            assert.deepEqual(JSON.parse(options.body), {
                connectionId: 'connection', deviceId: 'device', deviceNumber: 42, secret: registration.secret
            });
            return { ok: true, json: async () => ({ protocolVersion: 1, state: 'configured',
                connectionId: 'connection', deviceId: 'device', deviceNumber: 42 }) };
        };
        assert.deepEqual(await provision(local, registration, 'connection'), { deviceId: 'device', deviceNumber: 42 });
        assert.equal(calls, 1);
        await assert.rejects(provision('https://external.example', registration, 'connection'));
        assert.equal(calls, 1);
    } finally { globalThis.fetch = original; }
});

test('repair is a separate explicit local request', async () => {
    const original = globalThis.fetch;
    const registration = { deviceId: 'device', deviceNumber: 42, secret: 'B'.repeat(64), grant: 'repair-grant' };
    try {
        globalThis.fetch = async (url, options) => {
            assert.equal(url, `${local}/v1/repair`);
            assert.equal(options.headers['X-Station-Grant'], 'repair-grant');
            return { ok: true, json: async () => ({ protocolVersion: 1, state: 'configured',
                connectionId: 'connection', deviceId: 'device', deviceNumber: 42 }) };
        };
        assert.deepEqual(await repair(local, registration, 'connection'), { deviceId: 'device', deviceNumber: 42 });
    } finally { globalThis.fetch = original; }
});
