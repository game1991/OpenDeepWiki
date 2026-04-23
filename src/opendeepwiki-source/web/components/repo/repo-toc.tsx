import type { RepoHeading } from "@/types/repository";

interface RepoTocProps {
  headings: RepoHeading[];
}

export function RepoToc({ headings }: RepoTocProps) {
  if (headings.length === 0) {
    return null;
  }

  return (
    <div className="sticky top-20 space-y-2 text-sm text-muted-foreground">
      <div className="text-xs font-semibold uppercase text-foreground">On this page</div>
      <nav className="space-y-1">
        {headings.map((heading) => (
          <a
            key={heading.id}
            href={`#${heading.id}`}
            className="block transition-colors hover:text-foreground"
            style={{ paddingLeft: (heading.level - 1) * 12 }}
          >
            {heading.text}
          </a>
        ))}
      </nav>
    </div>
  );
}
