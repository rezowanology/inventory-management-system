// Renders the Mermaid diagrams of docs/diagrams/*.md to PNG and PDF.
// Run:  npm run diagrams --prefix client      (needs internet once: Mermaid is loaded from a pinned CDN URL)
// If you are offline, export the diagrams manually from https://mermaid.live and note that in DECISIONS.md.
import fs from 'node:fs'
import path from 'node:path'
import { fileURLToPath } from 'node:url'
import { chromium } from '@playwright/test'

const MERMAID_URL = 'https://cdn.jsdelivr.net/npm/mermaid@11.4.1/dist/mermaid.min.js' // pinned version
const DIAGRAMS = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../docs/diagrams')

/** All ```mermaid blocks of a markdown file, in order. */
function mermaidBlocks(markdown: string): string[] {
  return [...markdown.replace(/\r\n/g, '\n').matchAll(/```mermaid\n([\s\S]*?)```/g)].map((m) => m[1])
}

const browser = await chromium.launch({ channel: 'msedge' })
let failed = false

for (const name of ['process-flow', 'er-diagram']) {
  const blocks = mermaidBlocks(fs.readFileSync(path.join(DIAGRAMS, `${name}.md`), 'utf8'))
  const page = await browser.newPage({ viewport: { width: 1600, height: 1000 } })
  await page.setContent('<html><body style="margin:24px;font-family:sans-serif"><div id="out"></div></body></html>')
  await page.addScriptTag({ url: MERMAID_URL })

  const errors: string[] = await page.evaluate(async (codes: string[]) => {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const mermaid = (window as any).mermaid
    mermaid.initialize({ startOnLoad: false, theme: 'default', securityLevel: 'loose', flowchart: { useMaxWidth: false }, er: { useMaxWidth: false } })
    const problems: string[] = []
    for (let i = 0; i < codes.length; i++) {
      try {
        const { svg } = await mermaid.render(`diagram${i}`, codes[i])
        const holder = document.createElement('div')
        holder.style.margin = '0 0 32px 0'
        holder.innerHTML = svg
        document.getElementById('out')!.appendChild(holder)
      } catch (e) {
        problems.push(`block ${i + 1}: ${(e as Error).message}`)
      }
    }
    return problems
  }, blocks)

  if (errors.length > 0) {
    failed = true
    console.error(`${name}.md has Mermaid syntax errors:\n  ${errors.join('\n  ')}`)
  } else {
    await page.screenshot({ path: path.join(DIAGRAMS, `${name}.png`), fullPage: true })
    const size = await page.evaluate(() => ({ w: document.body.scrollWidth, h: document.body.scrollHeight }))
    await page.pdf({ path: path.join(DIAGRAMS, `${name}.pdf`), width: `${size.w + 48}px`, height: `${size.h + 48}px`, printBackground: true })
    console.log(`${name}: ${blocks.length} diagram(s) rendered to ${name}.png and ${name}.pdf`)
  }
  await page.close()
}

await browser.close()
if (failed) process.exit(1)
