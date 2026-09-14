#ifndef __LIBUART_H__
#define __LIBUART_H__

#include <iostream>

void uart_init();
int uart_open(const char* port);

void uart_close(int port_fd);

#endif