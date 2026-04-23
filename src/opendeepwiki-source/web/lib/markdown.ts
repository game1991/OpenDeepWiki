import type { RepoHeading } from "@/types/repository";

function normalizeHeadingText(text: string) {
  return text
    .replace(/`/g, "")
    .replace(/\[(.*?)\]\([^)]*\)/g, "$1")
    .replace(/[*_~]/g, "")
    .replace(/\s+/g, " ")
    .trim();
}

export function slugifyHeading(text: string) {
  return normalizeHeadingText(text)
    .toLowerCase()
    .replace(/[^\p{L}\p{N}\s-]/gu, "")
    .replace(/\s+/g, "-")
    .replace(/-+/g, "-")
    .replace(/^-|-$/g, "");
}

export function createSlugger() {
  const counts = new Map<string, number>();
  return (text: string) => {
    const base = slugifyHeading(text) || "section";
    const current = counts.get(base) ?? 0;
    counts.set(base, current + 1);
    return current === 0 ? base : `${base}-${current}`;
  };
}

export function extractHeadings(markdown: string, maxLevel = 3): RepoHeading[] {
  const lines = markdown.split(/\r?\n/);
  const headings: RepoHeading[] = [];
  const slugger = createSlugger();
  let inCodeBlock = false;

  for (const line of lines) {
    const trimmed = line.trim();
    if (trimmed.startsWith("```")) {
      inCodeBlock = !inCodeBlock;
      continue;
    }

    if (inCodeBlock) {
      continue;
    }

    const match = /^(#{1,6})\s+(.+)$/.exec(trimmed);
    if (!match) {
      continue;
    }

    const level = match[1].length;
    if (level > maxLevel) {
      continue;
    }

    const text = normalizeHeadingText(match[2].replace(/#+$/, "").trim());
    if (!text) {
      continue;
    }

    headings.push({
      id: slugger(text),
      text,
      level,
    });
  }

  return headings;
}
