import { execFileSync } from 'child_process';
import path from 'path';

// Runs once before any test, but AFTER the webServer entries are up (Playwright starts those first).
// `dotnet run -- --seed` seeds and exits, so it can't be a webServer entry.
export default function globalSetup() {
  const repoRoot = path.resolve(__dirname, '..');

  console.log('[global-setup] starting Redis (docker compose up -d redis)...');
  execFileSync('docker', ['compose', '-f', path.join(repoRoot, 'docker-compose.yml'), 'up', '-d', 'redis'], {
    stdio: 'inherit'
  });

  console.log('[global-setup] seeding the database (dotnet run -- --seed)...');
  execFileSync(
    'dotnet',
    ['run', '--project', path.join(repoRoot, 'src/API/API.csproj'), '--', '--seed'],
    { stdio: 'inherit' }
  );

  console.log('[global-setup] done.');
}
