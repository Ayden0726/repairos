#!/usr/bin/env tsx
import { runBackup } from "../src/server/services/backup.service";

runBackup(null, "cli")
  .then((r) => {
    console.log(JSON.stringify(r, null, 2));
    process.exit(0);
  })
  .catch((err) => {
    console.error(err);
    process.exit(1);
  });
