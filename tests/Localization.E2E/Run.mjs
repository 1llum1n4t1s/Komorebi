import { spawnSync } from 'node:child_process';
import { mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, isAbsolute, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { parseArgs } from 'node:util';
import { createRequire } from 'node:module';

const repoRoot = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
export function runLocalizationCheck({
    bun = process.execPath,
    artifactParent = resolve(repoRoot, 'tests/CloneLongPaths.E2E/bin/localization-artifacts'),
    script = resolve(repoRoot, 'build/scripts/localization-check.js'),
} = {}) {
    if (!isAbsolute(artifactParent)) throw new Error('--artifacts には絶対パスを指定してください。');
    mkdirSync(artifactParent, { recursive: true });
    const artifactRoot = mkdtempSync(resolve(artifactParent, 'run-'));
    const preload = resolve(artifactRoot, 'fail-read.mjs');
    const writeAttemptsPath = resolve(artifactRoot, 'write-attempts.log');
    writeFileSync(writeAttemptsPath, '');

    // 実 CLI の fs 境界だけを隔離し、読込失敗時の exit 1 と書込抑止を観測する。
    writeFileSync(preload, `import { mock } from 'bun:test';
import { appendFileSync } from 'node:fs';
const writeAttemptsPath = ${JSON.stringify(writeAttemptsPath)};
const boundary = () => ({
            readFile: async () => { throw new Error('RERE_SYNTHETIC_READ_FAILURE'); },
            writeFile: async () => {
                appendFileSync(writeAttemptsPath, 'writeFile\\n');
                throw new Error('RERE_UNEXPECTED_WRITE');
            },
});
mock.module('fs-extra', boundary);
mock.module(${JSON.stringify(createRequire(resolve(repoRoot, 'build/scripts/localization-check.js')).resolve('fs-extra'))}, boundary);
`);

    const processResult = spawnSync(bun, ['--preload', preload, script], {
        cwd: repoRoot, encoding: 'utf8', windowsHide: true,
    });
    if (processResult.error) throw processResult.error;
    writeFileSync(resolve(artifactRoot, 'stdout.log'), processResult.stdout);
    writeFileSync(resolve(artifactRoot, 'stderr.log'), processResult.stderr);
    // 子が例外を握りつぶしても、同期書込みした呼出記録を親で確認する。
    const writeAttempts = readFileSync(writeAttemptsPath, 'utf8').split('\n').filter(Boolean).length;
    const passed = writeAttempts === 0 && processResult.status === 1
        && processResult.stderr.includes('RERE_SYNTHETIC_READ_FAILURE')
        && !processResult.stderr.includes('RERE_UNEXPECTED_WRITE');
    const result = {
        Scenario: 'localization-read-failure-before-write',
        ExitCode: processResult.status,
        Passed: passed,
        WriteAttempts: writeAttempts,
        ArtifactDirectory: artifactRoot,
        Script: script,
    };
    writeFileSync(resolve(artifactRoot, 'result.json'), JSON.stringify(result, null, 2));
    return result;
}

if (import.meta.main) {
    const { values } = parseArgs({ options: {
        // 本番 CI と同じ Bun CLI のモジュール境界を検証する。
        bun: { type: 'string', default: process.execPath },
        artifacts: { type: 'string' },
    } });
    const result = runLocalizationCheck({ bun: values.bun, artifactParent: values.artifacts });
    console.log(`Artifacts: ${result.ArtifactDirectory}`);
    if (!result.Passed) throw new Error(`ローカライズ CLI 失敗経路の検証に失敗しました: ${resolve(result.ArtifactDirectory, 'stderr.log')}`);
    console.log('localization-read-failure-before-write: PASS');
}
