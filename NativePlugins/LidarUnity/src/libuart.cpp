#include <iostream>
#include <fcntl.h>      // File control definitions
#include <termios.h>    // POSIX terminal control definitions
#include <unistd.h>     // UNIX standard function definitions
#include <cstring>      // For memset
#include "libuart.h"
#include "liblog.h"

#define TAG "FloorGame_LibUART"



int uart_open(const char* port) {
    int port_fd = open(port, O_RDWR | O_NOCTTY);

    if (port_fd == -1) {
//        std::cerr << "Error opening UART port " << port << "\n";
        LOGE(TAG, "Error opening UART port");
        return -1;
    }

    // Configure port
    termios options;
    tcgetattr(port_fd, &options);

    // Set baud rate
    cfsetispeed(&options, B230400);
    cfsetospeed(&options, B230400);

    // 8N1 Mode (8 data bits, No parity, 1 stop bit)
    options.c_cflag &= ~PARENB; // No parity
    options.c_cflag &= ~CSTOPB; // 1 stop bit
    options.c_cflag &= ~CSIZE;
    options.c_cflag |= CS8;     // 8 data bits

    // Raw input mode
    options.c_lflag &= ~(ICANON | ECHO | ECHOE | ISIG); // Raw input
    options.c_iflag &= ~(IXON | IXOFF | IXANY);         // No software flow control
    options.c_oflag &= ~OPOST;                          // Raw output

    options.c_cflag |= (CLOCAL | CREAD); // Enable receiver, local mode

    options.c_cc[VMIN]  = 1;  // block until at least 1 byte arrives
    options.c_cc[VTIME] = 0;  // no timeout

    tcsetattr(port_fd, TCSANOW, &options);

    return port_fd;
}
