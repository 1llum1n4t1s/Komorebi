const fs = require('fs-extra');
const path = require('path');
const xml2js = require('xml2js');

// テストや別の作業ツリーでは --repo で対象を指定する。
const { values } = require('node:util').parseArgs({ options: { repo: { type: 'string' } } });
const repoRoot = values.repo ? path.resolve(values.repo) : path.join(__dirname, '../../');
const localesDir = path.join(repoRoot, 'src/Resources/Locales');
const enUSFile = path.join(localesDir, 'en_US.axaml');
const outputFile = path.join(repoRoot, 'docs/TRANSLATION.md');

const parser = new xml2js.Parser();

async function parseXml(filePath) {
    const data = await fs.readFile(filePath);
    return parser.parseStringPromise(data);
}

function filterAndSortTranslations(localeData, enUSKeyOrder) {
    const strings = localeData.ResourceDictionary['x:String'];
    // Remove keys that don't exist in English file
    const filtered = strings.filter(item => enUSKeyOrder.has(item.$['x:Key']));

    // Sort based on the key order in English file
    filtered.sort((a, b) => {
        const aIndex = enUSKeyOrder.get(a.$['x:Key']);
        const bIndex = enUSKeyOrder.get(b.$['x:Key']);
        return aIndex - bIndex;
    });

    return filtered;
}

async function calculateTranslationRate() {
    const enUSData = await parseXml(enUSFile);
    const enUSKeys = new Set(enUSData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));
    // 上流との差分: 並べ替えごとの線形検索を避け、全ロケールで同じ索引を共有する。
    const enUSKeyOrder = new Map([...enUSKeys].map((key, index) => [key, index]));
    const files = (await fs.readdir(localesDir)).filter(file => file !== 'en_US.axaml' && file.endsWith('.axaml')).sort();

    const lines = [];
    const pendingWrites = [];

    lines.push('# Translation Status');
    lines.push('This document shows the translation status of each locale file in the repository.');
    lines.push(`## Details`);
    lines.push(`### ![en_US](https://img.shields.io/badge/en__US-%E2%88%9A-brightgreen)`);

    for (const file of files) {
        const locale = file.replace('.axaml', '').replace('_', '__');
        const filePath = path.join(localesDir, file);
        const localeData = await parseXml(filePath);
        const localeKeys = new Set(localeData.ResourceDictionary['x:String'].map(item => item.$['x:Key']));
        const missingKeys = [...enUSKeys].filter(key => !localeKeys.has(key));

        // Sort and clean up extra translations
        const sortedAndCleaned = filterAndSortTranslations(localeData, enUSKeyOrder);
        localeData.ResourceDictionary['x:String'] = sortedAndCleaned;

        // 全ロケールの読込・解析・生成が成功してからまとめて書き込む。
        const builder = new xml2js.Builder({
            headless: true,
            renderOpts: { pretty: true, indent: '  ' }
        });
        let xmlStr = builder.buildObject(localeData);

        // Add an empty line before the first x:String
        xmlStr = xmlStr.replace('  <x:String', '\n  <x:String');
        pendingWrites.push([filePath, xmlStr + '\n']);

        if (missingKeys.length > 0) {
            const progress = ((enUSKeys.size - missingKeys.length) / enUSKeys.size) * 100;
            const badgeColor = progress >= 75 ? 'yellow' : 'red';

            lines.push(`### ![${locale}](https://img.shields.io/badge/${locale}-${progress.toFixed(2)}%25-${badgeColor})`);
            lines.push(`<details>\n<summary>Missing keys in ${file}</summary>\n\n${missingKeys.map(key => `- ${key}`).join('\n')}\n\n</details>`)
        } else {
            lines.push(`### ![${locale}](https://img.shields.io/badge/${locale}-%E2%88%9A-brightgreen)`);
        }
    }

    const content = lines.join('\n\n');
    console.log(content);
    pendingWrites.push([outputFile, content]);
    // 書込中の障害に対する原子性は保証しない。
    for (const [filePath, data] of pendingWrites) await fs.writeFile(filePath, data, 'utf8');
}

calculateTranslationRate().catch(err => {
    console.error(err);
    process.exitCode = 1;
});
