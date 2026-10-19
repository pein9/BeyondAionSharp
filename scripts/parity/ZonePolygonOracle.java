import java.awt.geom.Path2D;
import java.awt.geom.Rectangle2D;
import java.nio.file.Path;
import java.util.*;
import javax.xml.parsers.DocumentBuilderFactory;
import org.w3c.dom.*;

/** Standalone oracle for the straight-edge Path2D.Float used by Java 2b097879f. */
public class ZonePolygonOracle {
    record Shape(String name, float[][] points) {}

    public static void main(String[] args) throws Exception {
        List<Shape> shapes = new ArrayList<>(List.of(
            new Shape("square", new float[][] {{0,0},{256,0},{256,256},{0,256}}),
            new Shape("square-reversed", new float[][] {{0,256},{256,256},{256,0},{0,0}}),
            new Shape("twice-wound-square", new float[][] {{0,0},{256,0},{256,256},{0,256},{0,0},{256,0},{256,256},{0,256}}),
            new Shape("nested-same-winding", new float[][] {{0,0},{512,0},{512,512},{0,512},{0,0},{128,128},{384,128},{384,384},{128,384},{128,128},{0,0}}),
            new Shape("nested-opposite-winding", new float[][] {{0,0},{512,0},{512,512},{0,512},{0,0},{128,128},{128,384},{384,384},{384,128},{128,128},{0,0}}),
            new Shape("self-intersecting", new float[][] {{0,0},{256,256},{0,256},{256,0}}),
            new Shape("concave", new float[][] {{0,0},{256,0},{256,64},{64,64},{64,256},{0,256}}),
            new Shape("collinear", new float[][] {{0,0},{128,128},{256,256}}),
            new Shape("zero-area", new float[][] {{128,128},{128,128},{128,128}}),
            new Shape("large-float-coordinates", new float[][] {{100000000,100000000},{100000256,100000000},{100000256,100000256},{100000000,100000256}}),
            new Shape("mixed-float-extents", new float[][] {{-10000000000f,0},{0.00001f,0},{0.00001f,256},{-10000000000f,256}}),
            new Shape("nan-first-vertex", new float[][] {{Float.NaN,0},{256,0},{256,256},{0,256}}),
            new Shape("nan-last-vertex", new float[][] {{0,0},{256,0},{256,256},{Float.NaN,256}})
        ));
        // Include boundaries from shipped Altgard polygon zones as fixed historical oracle inputs.
        Document document = DocumentBuilderFactory.newInstance().newDocumentBuilder().parse(
            Path.of(args[0], "game-server/data/static_data/zones/zones_220030000.xml").toFile());
        NodeList zones = document.getElementsByTagName("zone");
        for (int i = 0, added = 0; i < zones.getLength() && added < 3; i++) {
            Element zone = (Element)zones.item(i);
            NodeList points = zone.getElementsByTagName("point");
            if (points.getLength() < 3) continue;
            float[][] data = new float[points.getLength()][2];
            for (int j = 0; j < points.getLength(); j++) {
                Element point = (Element)points.item(j);
                data[j] = new float[] {Float.parseFloat(point.getAttribute("x")), Float.parseFloat(point.getAttribute("y"))};
            }
            shapes.add(new Shape(zone.getAttribute("name"), data));
            added++;
        }

        StringJoiner output = new StringJoiner(",\n", "{\"upstreamSha\":\"2b097879f4355e2d32aec5e9efae38e5f83e8eb9\",\"oracle\":\"JDK " + System.getProperty("java.version") + " Path2D.Float WIND_NON_ZERO\",\"regionSize\":128,\"shapes\":[\n", "\n]}\n");
        Random random = new Random(206);
        for (Shape shape : shapes) {
            Path2D.Float path = new Path2D.Float(Path2D.WIND_NON_ZERO, shape.points.length);
            path.moveTo(shape.points[0][0], shape.points[0][1]);
            for (int i = 1; i < shape.points.length; i++) path.lineTo(shape.points[i][0], shape.points[i][1]);
            Rectangle2D bounds = path.getBounds2D();
            path.closePath();
            List<float[]> queries = new ArrayList<>();
            for (int i = 0; i < shape.points.length; i++) {
                queries.add(shape.points[i]);
                float[] a = shape.points[i], b = shape.points[(i + 1) % shape.points.length];
                queries.add(new float[] {(a[0]+b[0]) / 2, (a[1]+b[1]) / 2});
            }
            for (float x : new float[] {-128,0,1,64,128,256,512})
                for (float y : new float[] {-128,0,1,64,128,256,512}) queries.add(new float[] {x,y});
            if (Double.isFinite(bounds.getX()) && Double.isFinite(bounds.getY())) {
                for (int i = 0; i < 100; i++) queries.add(new float[] {
                    (float)(bounds.getX() - 128 + random.nextDouble() * (bounds.getWidth() + 256)),
                    (float)(bounds.getY() - 128 + random.nextDouble() * (bounds.getHeight() + 256))});
            }
            queries.add(new float[] {Float.NaN,0});
            queries.add(new float[] {Float.POSITIVE_INFINITY,0});
            queries.add(new float[] {Float.NEGATIVE_INFINITY,0});
            StringJoiner pointJson = new StringJoiner(",", "[", "]");
            for (float[] p : shape.points) pointJson.add("[" + number(p[0]) + "," + number(p[1]) + "]");
            StringJoiner queryJson = new StringJoiner(",\n", "[\n", "\n]");
            for (float[] p : queries) queryJson.add("{\"x\":" + number(p[0]) + ",\"y\":" + number(p[1]) +
                ",\"inside\":" + (bounds.contains(p[0],p[1]) && path.contains(p[0],p[1])) +
                ",\"intersects\":" + (bounds.intersects(p[0],p[1],128,128) && path.intersects(p[0],p[1],128,128)) + "}");
            output.add("{\"name\":\"" + shape.name + "\",\"points\":" + pointJson + ",\"queries\":" + queryJson + "}");
        }
        System.out.print(output);
    }

    private static String number(float value) {
        return Float.isFinite(value) ? Float.toString(value) : "\"" + value + "\"";
    }
}
