import { expect, test } from 'bun:test';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { runLocalizationCheck } from './Run.mjs';

test('書込み例外を握りつぶす CLI でも書込み試行を検出する', () => {
    const artifactParent = process.env.KOMOREBI_LOCALIZATION_ARTIFACTS
        ?? resolve(import.meta.dir, '../CloneLongPaths.E2E/bin/localization-artifacts');
    mkdirSync(artifactParent, { recursive: true });
    const fixtureRoot = mkdtempSync(resolve(artifactParent, 'caught-write-'));
    const script = resolve(fixtureRoot, 'caught-write.cjs');
    writeFileSync(script, `const fs = require('fs-extra');
(async () => {
    try { await fs.writeFile('unused', 'unused'); } catch {}
    try { await fs.readFile('unused'); }
    catch (error) { console.error(error.message); process.exitCode = 1; }
})();
`);
    const result = runLocalizationCheck({ artifactParent, script });
    const stderr = readFileSync(resolve(result.ArtifactDirectory, 'stderr.log'), 'utf8');
    expect(result.ExitCode).toBe(1);
    expect(stderr).toContain('RERE_SYNTHETIC_READ_FAILURE');
    expect(stderr).not.toContain('RERE_UNEXPECTED_WRITE');
    expect(result.WriteAttempts).toBe(1);
    expect(result.Passed).toBe(false);
});
