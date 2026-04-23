// @ts-nocheck
import * as __fd_glob_25 from "../content/docs/mcp/index.mdx?collection=docs"
import * as __fd_glob_24 from "../content/docs/getting-started/windows-deployment.mdx?collection=docs"
import * as __fd_glob_23 from "../content/docs/getting-started/local-development.mdx?collection=docs"
import * as __fd_glob_22 from "../content/docs/getting-started/index.mdx?collection=docs"
import * as __fd_glob_21 from "../content/docs/getting-started/docker-deployment.mdx?collection=docs"
import * as __fd_glob_20 from "../content/docs/deployment/docker-compose.mdx?collection=docs"
import * as __fd_glob_19 from "../content/docs/deployment/database.mdx?collection=docs"
import * as __fd_glob_18 from "../content/docs/deployment/ai-providers.mdx?collection=docs"
import * as __fd_glob_17 from "../content/docs/configuration/environment-variables.mdx?collection=docs"
import * as __fd_glob_16 from "../content/docs/api-reference/repositories.mdx?collection=docs"
import * as __fd_glob_15 from "../content/docs/api-reference/incremental-updates.mdx?collection=docs"
import * as __fd_glob_14 from "../content/docs/api-reference/chat.mdx?collection=docs"
import * as __fd_glob_13 from "../content/docs/api-reference/auth.mdx?collection=docs"
import * as __fd_glob_12 from "../content/docs/api-reference/admin.mdx?collection=docs"
import * as __fd_glob_11 from "../content/docs/architecture/index.mdx?collection=docs"
import * as __fd_glob_10 from "../content/docs/architecture/frontend.mdx?collection=docs"
import * as __fd_glob_9 from "../content/docs/architecture/data-models.mdx?collection=docs"
import * as __fd_glob_8 from "../content/docs/architecture/backend.mdx?collection=docs"
import * as __fd_glob_7 from "../content/docs/index.mdx?collection=docs"
import { default as __fd_glob_6 } from "../content/docs/getting-started/meta.json?collection=docs"
import { default as __fd_glob_5 } from "../content/docs/mcp/meta.json?collection=docs"
import { default as __fd_glob_4 } from "../content/docs/architecture/meta.json?collection=docs"
import { default as __fd_glob_3 } from "../content/docs/deployment/meta.json?collection=docs"
import { default as __fd_glob_2 } from "../content/docs/configuration/meta.json?collection=docs"
import { default as __fd_glob_1 } from "../content/docs/api-reference/meta.json?collection=docs"
import { default as __fd_glob_0 } from "../content/docs/meta.json?collection=docs"
import { server } from 'fumadocs-mdx/runtime/server';
import type * as Config from '../source.config';

const create = server<typeof Config, import("fumadocs-mdx/runtime/types").InternalTypeConfig & {
  DocData: {
  }
}>({"doc":{"passthroughs":["extractedReferences"]}});

export const docs = await create.docs("docs", "content/docs", {"meta.json": __fd_glob_0, "api-reference/meta.json": __fd_glob_1, "configuration/meta.json": __fd_glob_2, "deployment/meta.json": __fd_glob_3, "architecture/meta.json": __fd_glob_4, "mcp/meta.json": __fd_glob_5, "getting-started/meta.json": __fd_glob_6, }, {"index.mdx": __fd_glob_7, "architecture/backend.mdx": __fd_glob_8, "architecture/data-models.mdx": __fd_glob_9, "architecture/frontend.mdx": __fd_glob_10, "architecture/index.mdx": __fd_glob_11, "api-reference/admin.mdx": __fd_glob_12, "api-reference/auth.mdx": __fd_glob_13, "api-reference/chat.mdx": __fd_glob_14, "api-reference/incremental-updates.mdx": __fd_glob_15, "api-reference/repositories.mdx": __fd_glob_16, "configuration/environment-variables.mdx": __fd_glob_17, "deployment/ai-providers.mdx": __fd_glob_18, "deployment/database.mdx": __fd_glob_19, "deployment/docker-compose.mdx": __fd_glob_20, "getting-started/docker-deployment.mdx": __fd_glob_21, "getting-started/index.mdx": __fd_glob_22, "getting-started/local-development.mdx": __fd_glob_23, "getting-started/windows-deployment.mdx": __fd_glob_24, "mcp/index.mdx": __fd_glob_25, });