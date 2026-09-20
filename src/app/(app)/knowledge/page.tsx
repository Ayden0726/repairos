import { requirePermission } from "@/server/actor";
import { prisma } from "@/server/db";
import { Input } from "@/components/ui/input";

export default async function KnowledgePage({ searchParams }: { searchParams: Promise<{ q?: string }> }) {
  await requirePermission("knowledge.view");
  const { q } = await searchParams;
  const articles = await prisma.knowledgeArticle.findMany({
    where: q
      ? { OR: [{ title: { contains: q, mode: "insensitive" } }, { body: { contains: q, mode: "insensitive" } }], archivedAt: null }
      : { archivedAt: null },
    include: { category: true, author: true },
    orderBy: { updatedAt: "desc" },
  });
  return (
    <div className="space-y-4">
      <h1 className="text-2xl font-semibold">Knowledge base</h1>
      <p className="text-sm text-muted-foreground">Internal notes and authorised manuals only. Do not paste proprietary OEM manuals without permission.</p>
      <form>
        <Input name="q" defaultValue={q} placeholder="Search articles and tags" />
      </form>
      <ul className="space-y-3">
        {articles.map((a) => (
          <li key={a.id} className="rounded-xl border p-4">
            <h2 className="font-medium">{a.title}</h2>
            <p className="text-xs text-muted-foreground">
              {a.category?.name} · {a.author.name} · {a.tags.join(", ")}
            </p>
            <p className="mt-2 text-sm whitespace-pre-wrap">{a.body}</p>
          </li>
        ))}
      </ul>
    </div>
  );
}
