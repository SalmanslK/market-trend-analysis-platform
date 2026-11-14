/**
 * The brief comes as a small Markdown subset: "## " headings, paragraphs, "- " bullets and **bold**.
 * This splits it into blocks the page renders; anything else is shown as plain text.
 */
export type BriefBlock =
  | { kind: 'heading'; text: string }
  | { kind: 'paragraph'; text: string }
  | { kind: 'list'; items: string[] }

export function parseBrief(text: string): BriefBlock[] {
  const blocks: BriefBlock[] = []
  let paragraph: string[] = []
  let list: string[] | null = null

  const flush = () => {
    if (paragraph.length) blocks.push({ kind: 'paragraph', text: paragraph.join(' ') })
    if (list) blocks.push({ kind: 'list', items: list })
    paragraph = []
    list = null
  }

  for (const raw of text.replace(/\r/g, '').split('\n')) {
    const line = raw.trim()
    if (!line) { flush(); continue }
    const heading = line.match(/^#{1,4}\s+(.*)$/)
    if (heading) { flush(); blocks.push({ kind: 'heading', text: heading[1] }); continue }
    const bullet = line.match(/^[-*•]\s+(.*)$/)
    if (bullet) {
      if (paragraph.length) { blocks.push({ kind: 'paragraph', text: paragraph.join(' ') }); paragraph = [] }
      list = list ?? []
      list.push(bullet[1])
      continue
    }
    if (list) { blocks.push({ kind: 'list', items: list }); list = null }
    paragraph.push(line)
  }
  flush()
  return blocks
}

/** Splits a line on **bold** markers: odd parts are bold. */
export function boldParts(text: string): { text: string; bold: boolean }[] {
  return text.split('**').map((part, i) => ({ text: part, bold: i % 2 === 1 })).filter((p) => p.text.length > 0)
}
