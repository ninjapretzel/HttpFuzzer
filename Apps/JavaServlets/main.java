
import java.io.*;
import javax.servlet.*;
import javax.servlet.http.*;

// I'm throwing an error with importing javax.servlet

class FuzzerServer extends HttpServlet {

    public void init() throws ServletException {
        // automatically starts listening
    }

    public void doGet(HttpServletRequest request, HttpServletResponse response) throws ServletException, IOException {
        System.out.println("Hello world!");
    }

    public void destroy() {

    }
}

