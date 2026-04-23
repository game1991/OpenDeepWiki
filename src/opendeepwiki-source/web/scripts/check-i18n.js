// 检查i18n翻译文件的完整性
const fs = require('fs');
const path = require('path');

const locales = ['zh', 'en', 'ja', 'ko'];
const messagesDir = path.join(__dirname, '../i18n/messages');

console.log('🔍 检查i18n翻译文件...\n');

// 检查每个语言的翻译文件
locales.forEach(locale => {
  const localeDir = path.join(messagesDir, locale);
  console.log(`📁 ${locale}:`);
  
  if (!fs.existsSync(localeDir)) {
    console.log(`  ❌ 目录不存在: ${localeDir}`);
    return;
  }
  
  const files = fs.readdirSync(localeDir).filter(f => f.endsWith('.json'));
  files.forEach(file => {
    const filePath = path.join(localeDir, file);
    try {
      const content = JSON.parse(fs.readFileSync(filePath, 'utf8'));
      const keys = Object.keys(content);
      console.log(`  ✅ ${file} (${keys.length} keys)`);
    } catch (err) {
      console.log(`  ❌ ${file} - 解析错误: ${err.message}`);
    }
  });
  console.log('');
});

// 检查request.ts
const requestPath = path.join(__dirname, '../i18n/request.ts');
console.log('📄 检查 request.ts...');
if (fs.existsSync(requestPath)) {
  const content = fs.readFileSync(requestPath, 'utf8');
  const imports = content.match(/await import\(`\.\/messages\/\$\{locale\}\/(.+?)\.json`\)/g);
  if (imports) {
    console.log('  加载的翻译文件:');
    imports.forEach(imp => {
      const match = imp.match(/\/([^/]+)\.json/);
      if (match) {
        console.log(`    - ${match[1]}.json`);
      }
    });
  }
  console.log('  ✅ request.ts 存在');
} else {
  console.log('  ❌ request.ts 不存在');
}

console.log('\n✨ 检查完成！');
console.log('\n💡 如果翻译没有生效，请尝试：');
console.log('   1. 重启开发服务器: npm run dev');
console.log('   2. 清除浏览器缓存并硬刷新 (Ctrl+Shift+R)');
console.log('   3. 删除 .next 目录: rm -rf .next');
