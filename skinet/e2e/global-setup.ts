import { execFileSync } from 'child_process';
import path from 'path';

// Playwright runs this once, before it starts the app, and before any test.
// We use it to get the app's dependencies ready: Redis running, database seeded.
// `dotnet run -- --seed` seeds data then exits (it does not serve the app) --
// that's why it runs here and not as one of the webServer entries below.
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
