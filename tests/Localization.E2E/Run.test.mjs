import { expect, test } from 'bun:test';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { spawnSync } from 'node:child_process';
import { createRequire } from 'node:module';
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

for (const input of ['missing', 'malformed']) {
    test(`実ファイルの ${input} 英語XMLで exit 1、翻訳と文書を変更しない`, () => {
        const parent = process.env.KOMOREBI_LOCALIZATION_ARTIFACTS
            ?? resolve(import.meta.dir, '../CloneLongPaths.E2E/bin/localization-artifacts');
        mkdirSync(parent, { recursive: true });
        const root = mkdtempSync(resolve(parent, 'filesystem-'));
        const locales = resolve(root, 'src/Resources/Locales');
        mkdirSync(locales, { recursive: true });
        mkdirSync(resolve(root, 'docs'));
        const translation = resolve(locales, 'ja_JP.axaml');
        const document = resolve(root, 'docs/TRANSLATION.md');
        writeFileSync(translation, 'TRANSLATION_SENTINEL');
        writeFileSync(document, 'DOCUMENT_SENTINEL');
        if (input === 'malformed') writeFileSync(resolve(locales, 'en_US.axaml'), '<ResourceDictionary>');
        const result = spawnSync(process.execPath, [resolve(import.meta.dir, '../../build/scripts/localization-check.js'), '--repo', root], { encoding: 'utf8' });
        if (result.error) throw result.error;
        expect(result.status).toBe(1);
        expect(result.stderr).toContain(input === 'missing' ? 'ENOENT' : 'Unclosed root tag');
        expect(readFileSync(translation, 'utf8')).toBe('TRANSLATION_SENTINEL');
        expect(readFileSync(document, 'utf8')).toBe('DOCUMENT_SENTINEL');
    });
}

for (const failure of ['malformed', 'unreadable']) {
    test(`後続localeが ${failure} でも先行の整形候補を含め書込試行0`, () => {
        const parent = process.env.KOMOREBI_LOCALIZATION_ARTIFACTS
            ?? resolve(import.meta.dir, '../CloneLongPaths.E2E/bin/localization-artifacts');
        mkdirSync(parent, { recursive: true });
        const root = mkdtempSync(resolve(parent, 'late-failure-'));
        const locales = resolve(root, 'src/Resources/Locales');
        mkdirSync(locales, { recursive: true });
        mkdirSync(resolve(root, 'docs'));
        const xml = '<ResourceDictionary xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"><x:String x:Key="A">a</x:String></ResourceDictionary>';
        writeFileSync(resolve(locales, 'en_US.axaml'), xml);
        const earlier = resolve(locales, 'aa_AA.axaml');
        writeFileSync(earlier, xml);
        if (failure === 'malformed') writeFileSync(resolve(locales, 'zz_ZZ.axaml'), '<ResourceDictionary>');
        else mkdirSync(resolve(locales, 'zz_ZZ.axaml'));
        const document = resolve(root, 'docs/TRANSLATION.md');
        writeFileSync(document, 'DOCUMENT_SENTINEL');
        const writes = resolve(root, 'writes.log');
        writeFileSync(writes, '');
        const preload = resolve(root, 'observe-writes.mjs');
        const script = resolve(import.meta.dir, '../../build/scripts/localization-check.js');
        const modulePath = createRequire(script).resolve('fs-extra');
        writeFileSync(preload, `import { mock } from 'bun:test';
import * as fs from 'node:fs/promises';
import { appendFileSync } from 'node:fs';
mock.module(${JSON.stringify(modulePath)}, () => ({
    readFile: fs.readFile,
    readdir: async () => ['aa_AA.axaml', 'zz_ZZ.axaml'],
    writeFile: async (...args) => {
        appendFileSync(${JSON.stringify(writes)}, 'write\\n');
        return fs.writeFile(...args);
    },
}));`);
        const result = spawnSync(process.execPath, ['--preload', preload, script, '--repo', root], { encoding: 'utf8' });
        if (result.error) throw result.error;
        expect(result.status).toBe(1);
        expect(result.stderr).toContain(failure === 'malformed' ? 'Unclosed root tag' : 'EISDIR');
        expect(readFileSync(writes, 'utf8')).toBe('');
        expect(readFileSync(earlier, 'utf8')).toBe(xml);
        expect(readFileSync(document, 'utf8')).toBe('DOCUMENT_SENTINEL');
    });
}
