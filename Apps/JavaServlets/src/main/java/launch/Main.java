package launch;
import java.io.*;
import javax.servlet.*;
import javax.servlet.http.*;
import org.apache.catalina.Context;
import org.apache.catalina.startup.Tomcat;


public class Main {
	
	public static void main(String[] args) {
		
		Tomcat tomcat = new Tomcat();
		int port = 3000;
		
		tomcat.setPort(3000);
		
		
		tomcat.setBaseDir("temp");
		Context context = tomcat.addContext(("/"), System.getProperty(("user.dir")));
		
		tomcat.addServlet("/", "SimpleServlet", new SimpleServlet());
		context.addServletMappingDecoded("/*", "SimpleServlet");
		
		try {
			tomcat.start();
		} catch (Exception e) {
			System.err.println("Error starting tomcat:");
			System.err.println(e);
			return;
		}
		
		tomcat.getServer().await();
		
	}
	
}

class SimpleServlet extends HttpServlet {
	protected void doGet(HttpServletRequest req, HttpServletResponse res) throws ServletException, IOException {
		doIt(req, res);
	}
	
	public static void doIt(HttpServletRequest req, HttpServletResponse res) throws ServletException, IOException {
		PrintWriter writer = res.getWriter();
		writer.println("{\"success\":1}");
	}
	
}
