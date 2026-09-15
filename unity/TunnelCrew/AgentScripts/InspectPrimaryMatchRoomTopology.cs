// Read-only Unity preflight for Primary Match room blueprints.
// Run only after coordinating Editor access. It writes a report outside Assets/ and
// never spawns, clears, damages, or otherwise mutates runtime/world objects.

System.Func<System.Collections.Generic.IEnumerable<UnityEngine.Vector2Int>, string> cellsJson = cells =>
    "[" + string.Join(",", System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(cells,
        cell => "{\"c\":" + cell.x + ",\"r\":" + cell.y + "}"))) + "]";

var bootstrap = UnityEngine.Object.FindFirstObjectByType<TunnelCrew.Presentation.RunBootstrap>();
if (bootstrap == null || bootstrap.Sim == null || bootstrap.Sim.World == null)
    throw new System.Exception("A running TunnelCrew world is required for the read-only topology preflight.");

var world = bootstrap.Sim.World;
var planPath = System.IO.Path.Combine(
    System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")),
    "AgentScripts", "primary-match-bold-import-plan.json");
if (!System.IO.File.Exists(planPath)) throw new System.Exception("Primary Match import plan missing: " + planPath);
var plan = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsMap(
    TunnelCrew.EditorTools.ArtPipeline.MiniJson.Parse(System.IO.File.ReadAllText(planPath), out var planError));
if (plan == null || planError != null) throw new System.Exception("Primary Match import plan parse failed: " + planError);
var blueprints = TunnelCrew.EditorTools.ArtPipeline.MiniJson.AsList(plan["roomBlueprints"]);
if (blueprints == null || blueprints.Count != 3)
    throw new System.Exception("Expected three dormant room blueprints before topology inspection.");

int startC = world.EntryCol;
int startR = world.EntryRow;
if (world.IsSolid(startC, startR))
{
    bool found = false;
    for (int radius = 1; radius < System.Math.Max(world.Cols, world.Rows) && !found; radius++)
        for (int r = System.Math.Max(0, startR - radius); r <= System.Math.Min(world.Rows - 1, startR + radius) && !found; r++)
            for (int c = System.Math.Max(0, startC - radius); c <= System.Math.Min(world.Cols - 1, startC + radius); c++)
                if (!world.IsSolid(c, r)) { startC = c; startR = r; found = true; break; }
    if (!found) throw new System.Exception("No open cell exists in the current world.");
}

var connected = new bool[world.CellCount];
var queue = new System.Collections.Generic.Queue<int>();
int startIndex = world.Index(startC, startR);
connected[startIndex] = true;
queue.Enqueue(startIndex);
var directions = new[] { UnityEngine.Vector2Int.right, UnityEngine.Vector2Int.left,
                         UnityEngine.Vector2Int.up, UnityEngine.Vector2Int.down };
while (queue.Count > 0)
{
    int index = queue.Dequeue();
    int c = index % world.Cols;
    int r = index / world.Cols;
    foreach (var direction in directions)
    {
        int nc = c + direction.x;
        int nr = r + direction.y;
        if (!world.InBounds(nc, nr) || world.IsSolid(nc, nr)) continue;
        int next = world.Index(nc, nr);
        if (connected[next]) continue;
        connected[next] = true;
        queue.Enqueue(next);
    }
}

var open = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
var northWallFoot = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
var westWallFoot = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
var eastWallFoot = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
var southBoundary = new System.Collections.Generic.List<UnityEngine.Vector2Int>();
for (int r = 0; r < world.Rows; r++)
    for (int c = 0; c < world.Cols; c++)
    {
        if (!connected[world.Index(c, r)]) continue;
        var cell = new UnityEngine.Vector2Int(c, r);
        open.Add(cell);
        if (world.IsSolid(c, r + 1)) northWallFoot.Add(cell);
        if (world.IsSolid(c - 1, r)) westWallFoot.Add(cell);
        if (world.IsSolid(c + 1, r)) eastWallFoot.Add(cell);
        if (world.IsSolid(c, r - 1)) southBoundary.Add(cell);
    }

double centerC = open.Count > 0 ? System.Linq.Enumerable.Average(open, cell => (double)cell.x) : startC;
double centerR = open.Count > 0 ? System.Linq.Enumerable.Average(open, cell => (double)cell.y) : startR;
System.Func<System.Collections.Generic.IEnumerable<UnityEngine.Vector2Int>,
            System.Collections.Generic.IEnumerable<UnityEngine.Vector2Int>> rank = cells =>
    System.Linq.Enumerable.Take(
        System.Linq.Enumerable.OrderBy(cells,
            cell => System.Math.Abs(cell.x - centerC) + System.Math.Abs(cell.y - centerR)), 12);

int bestX = 0, bestY = 0, bestWidth = 0, bestHeight = 0, bestArea = 0;
var heights = new int[world.Cols];
for (int r = 0; r < world.Rows; r++)
{
    for (int c = 0; c < world.Cols; c++)
        heights[c] = connected[world.Index(c, r)] ? heights[c] + 1 : 0;
    for (int left = 0; left < world.Cols; left++)
    {
        int minimumHeight = int.MaxValue;
        for (int right = left; right < world.Cols; right++)
        {
            minimumHeight = System.Math.Min(minimumHeight, heights[right]);
            if (minimumHeight == 0) break;
            int width = right - left + 1;
            int area = width * minimumHeight;
            if (area <= bestArea) continue;
            bestArea = area;
            bestX = left;
            bestY = r - minimumHeight + 1;
            bestWidth = width;
            bestHeight = minimumHeight;
        }
    }
}

string planId = TunnelCrew.EditorTools.ArtPipeline.MiniJson.GetString(plan, "planId");
var json = new System.Text.StringBuilder();
json.AppendLine("{");
json.AppendLine("  \"status\": \"inspected_read_only_no_runtime_mutation\",");
json.AppendLine("  \"planId\": \"" + planId + "\",");
json.AppendLine("  \"unityVersion\": \"" + UnityEngine.Application.unityVersion + "\",");
json.AppendLine("  \"world\": {");
json.AppendLine("    \"depth\": " + bootstrap.Sim.Depth + ",");
json.AppendLine("    \"cols\": " + world.Cols + ", \"rows\": " + world.Rows + ",");
json.AppendLine("    \"worldVersionBeforeAndAfter\": [" + world.Version + ", " + world.Version + "],");
json.AppendLine("    \"entryCell\": {\"c\":" + world.EntryCol + ",\"r\":" + world.EntryRow + "},");
json.AppendLine("    \"connectedOpenCells\": " + open.Count);
json.AppendLine("  },");
json.AppendLine("  \"largestConnectedOpenRectangle\": {");
json.AppendLine("    \"x\": " + bestX + ", \"y\": " + bestY + ", \"width\": " + bestWidth + ", \"height\": " + bestHeight + ",");
json.AppendLine("    \"widthFractionOfWorld\": " + (world.Cols > 0 ? ((double)bestWidth / world.Cols).ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture) : "0") + ",");
json.AppendLine("    \"areaCells\": " + bestArea);
json.AppendLine("  },");
json.AppendLine("  \"candidateAnchors\": {");
json.AppendLine("    \"northWallFootCount\": " + northWallFoot.Count + ", \"northWallFoot\": " + cellsJson(rank(northWallFoot)) + ",");
json.AppendLine("    \"westWallFootCount\": " + westWallFoot.Count + ", \"westWallFoot\": " + cellsJson(rank(westWallFoot)) + ",");
json.AppendLine("    \"eastWallFootCount\": " + eastWallFoot.Count + ", \"eastWallFoot\": " + cellsJson(rank(eastWallFoot)) + ",");
json.AppendLine("    \"southBoundaryCount\": " + southBoundary.Count + ", \"southBoundary\": " + cellsJson(rank(southBoundary)));
json.AppendLine("  },");
json.AppendLine("  \"roomBlueprintCount\": " + blueprints.Count + ",");
json.AppendLine("  \"runtimeMutationPerformed\": false");
json.AppendLine("}");

var outputPath = System.IO.Path.Combine(
    System.IO.Path.GetFullPath(System.IO.Path.Combine(UnityEngine.Application.dataPath, "..")),
    "AgentScripts", "primary-match-room-topology-preflight.json");
System.IO.File.WriteAllText(outputPath, json.ToString());
UnityEngine.Debug.Log("[Primary Match Bold] Read-only topology preflight written: " + outputPath);
